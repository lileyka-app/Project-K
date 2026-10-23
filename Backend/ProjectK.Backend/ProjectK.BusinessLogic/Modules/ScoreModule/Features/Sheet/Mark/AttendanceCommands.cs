using FluentValidation;
using MediatR;
using ProjectK.BusinessLogic.Modules.ScoreModule.Features.Sheet.NotHeld;
using ProjectK.BusinessLogic.Modules.ScoreModule.Models;
using ProjectK.BusinessLogic.Modules.ScoreModule.Services;
using ProjectK.Common.Entities.ScoreModule;
using ProjectK.Common.Exceptions;
using ProjectK.Common.Interfaces;
using ProjectK.Common.Interfaces.Modules.InfrastructureModule;
using ProjectK.Common.Interfaces.Modules.KraiModule;
using ProjectK.Common.Interfaces.Modules.KurinModule;
using ProjectK.Common.Interfaces.Modules.ScoreModule;
using ProjectK.Common.Models.Dtos.ScoreModule.Requests;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Records;

namespace ProjectK.BusinessLogic.Modules.ScoreModule.Features.Sheet.Mark;

/// <summary>
/// Marks people present at one occurrence. Someone already marked stays marked by whoever did it
/// first — the result says so, and the unique index says so again for two судді who click at once.
/// The first mark takes back «did not take place», if someone said it of this occurrence.
/// </summary>
public sealed record MarkAttendanceCommand(Guid KurinKey, Guid AgendaItemKey, DateTime OccurrenceStartUtc, MarkAttendanceRequest Request)
    : IRequest<ServiceResult<IReadOnlyList<MarkAttendanceResultDto>>>;

public sealed class MarkAttendanceCommandValidator : AbstractValidator<MarkAttendanceCommand>
{
    public MarkAttendanceCommandValidator()
    {
        RuleFor(c => c.KurinKey).NotEmpty();
        RuleFor(c => c.AgendaItemKey).NotEmpty();
        RuleFor(c => c.Request.MembershipKeys).NotEmpty().Must(keys => keys.Count <= 500);
    }
}

public sealed class MarkAttendanceCommandHandler : IRequestHandler<MarkAttendanceCommand, ServiceResult<IReadOnlyList<MarkAttendanceResultDto>>>
{
    private readonly ScoreAccess _access;
    private readonly IScoreUnitOfWork _score;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMembershipDirectory _memberships;
    private readonly ScoreBookReader _books;
    private readonly ICurrentUserContext _currentUser;
    private readonly TimeProvider _time;
    private readonly IKraiProfileProvider _profiles;
    private readonly IKurinAudit _audit;

    public MarkAttendanceCommandHandler(
        ScoreAccess access,
        IScoreUnitOfWork score,
        IUnitOfWork unitOfWork,
        IMembershipDirectory memberships,
        ScoreBookReader books,
        ICurrentUserContext currentUser,
        TimeProvider time,
        IKraiProfileProvider profiles,
        IKurinAudit audit)
    {
        _access = access;
        _score = score;
        _unitOfWork = unitOfWork;
        _memberships = memberships;
        _books = books;
        _currentUser = currentUser;
        _time = time;
        _profiles = profiles;
        _audit = audit;
    }

    public async Task<ServiceResult<IReadOnlyList<MarkAttendanceResultDto>>> Handle(MarkAttendanceCommand request, CancellationToken cancellationToken)
    {
        if (_access.Refuse<IReadOnlyList<MarkAttendanceResultDto>>(request.KurinKey) is { } refused)
        {
            return refused;
        }

        var occurrence = await EventOccurrence.LoadAsync(_unitOfWork, _profiles, request.KurinKey, request.AgendaItemKey, request.OccurrenceStartUtc, cancellationToken);
        if (occurrence is null)
        {
            return ScoreAccess.NotFound<IReadOnlyList<MarkAttendanceResultDto>>("There is no such event, or it does not meet that day.");
        }

        var youths = (await _memberships.GetInKurinAsync(request.KurinKey, cancellationToken))
            .Where(m => m.Kind == MembershipKind.Youth && m.LeftAtUtc is null)
            .ToDictionary(m => m.MembershipKey);

        try
        {
            return await MarkAsync(request, occurrence, youths, cancellationToken);
        }
        catch (DuplicateRowException)
        {
            // Another judge marked one of these people between our check and our save. A second
            // pass sees their mark and answers AlreadyMarked for that person, and marks the rest.
            return await MarkAsync(request, occurrence, youths, cancellationToken);
        }
    }

    private async Task<ServiceResult<IReadOnlyList<MarkAttendanceResultDto>>> MarkAsync(
        MarkAttendanceCommand request,
        EventOccurrence occurrence,
        IReadOnlyDictionary<Guid, KurinMembershipRecord> youths,
        CancellationToken cancellationToken)
    {
        var occurrenceStartUtc = occurrence.StartUtc;
        var results = new List<MarkAttendanceResultDto>();
        var now = _time.GetUtcNow().UtcDateTime;
        ScoreBook? book = null;

        foreach (var membershipKey in request.Request.MembershipKeys.Distinct())
        {
            if (!youths.TryGetValue(membershipKey, out var membership))
            {
                return ScoreAccess.NotFound<IReadOnlyList<MarkAttendanceResultDto>>("That person is not a youth of this kurin.");
            }

            if (!await _access.MayScoreAsync(request.KurinKey, membership.GroupKey, ResourceAction.Create, cancellationToken))
            {
                return ScoreAccess.Forbidden<IReadOnlyList<MarkAttendanceResultDto>>();
            }

            var standing = await _score.ScoreAttendances.GetStandingAsync(membershipKey, request.AgendaItemKey, occurrenceStartUtc, cancellationToken);
            if (standing is not null)
            {
                book ??= await _books.OpenAsync(request.KurinKey, cancellationToken);
                results.Add(new MarkAttendanceResultDto { MembershipKey = membershipKey, Outcome = "AlreadyMarked", MarkedByName = book.NameOfAccount(standing.MarkedByUserKey) });
                continue;
            }

            var mark = new ScoreAttendance
            {
                KurinKey = request.KurinKey,
                MembershipKey = membershipKey,
                AgendaItemKey = request.AgendaItemKey,
                OccurrenceStartUtc = occurrenceStartUtc,
                MarkedByUserKey = _currentUser.UserId,
                MarkedAtUtc = now,
                CreatedDate = now,
                UpdatedDate = now
            };
            _score.ScoreAttendances.Create(mark, cancellationToken);
            ScoreTrail.Record(_score, request.KurinKey, ScoreTrail.Attendance, mark.ScoreAttendanceKey, ScoreTrail.Created, Snapshot(mark), _currentUser.UserId, now);
            results.Add(new MarkAttendanceResultDto { MembershipKey = membershipKey, Outcome = "Marked" });
        }

        // A meeting someone was at took place, whatever was said of it before.
        if (results.Any(r => r.Outcome == "Marked")
            && await _score.ScoreNotHeldOccurrences.FindAsync(request.AgendaItemKey, occurrenceStartUtc, cancellationToken) is { } notHeld)
        {
            _score.ScoreNotHeldOccurrences.Delete(notHeld, cancellationToken);
            await SetOccurrenceNotHeldCommandHandler.RecordAsync(_audit, request.KurinKey, occurrence, notHeld: false, cancellationToken);
        }

        await _score.SaveChangesAsync(cancellationToken);
        return new ServiceResult<IReadOnlyList<MarkAttendanceResultDto>>(ResultType.Success, results);
    }

    internal static object Snapshot(ScoreAttendance mark) => new
    {
        mark.MembershipKey,
        mark.AgendaItemKey,
        mark.OccurrenceStartUtc,
        mark.MarkedByUserKey,
        mark.MarkedAtUtc,
        mark.RemovedAtUtc,
        mark.RemovedByUserKey
    };
}

/// <summary>Takes a mark off. Anyone who may score the person's гурток may, whoever put it there.</summary>
public sealed record UnmarkAttendanceCommand(Guid KurinKey, Guid AgendaItemKey, DateTime OccurrenceStartUtc, Guid MembershipKey)
    : IRequest<ServiceResult<object>>;

public sealed class UnmarkAttendanceCommandHandler : IRequestHandler<UnmarkAttendanceCommand, ServiceResult<object>>
{
    private readonly ScoreAccess _access;
    private readonly IScoreUnitOfWork _score;
    private readonly IMembershipDirectory _memberships;
    private readonly ICurrentUserContext _currentUser;
    private readonly TimeProvider _time;

    public UnmarkAttendanceCommandHandler(ScoreAccess access, IScoreUnitOfWork score, IMembershipDirectory memberships, ICurrentUserContext currentUser, TimeProvider time)
    {
        _access = access;
        _score = score;
        _memberships = memberships;
        _currentUser = currentUser;
        _time = time;
    }

    public async Task<ServiceResult<object>> Handle(UnmarkAttendanceCommand request, CancellationToken cancellationToken)
    {
        if (_access.Refuse<object>(request.KurinKey) is { } refused)
        {
            return refused;
        }

        var mark = await _score.ScoreAttendances.GetStandingAsync(
            request.MembershipKey, request.AgendaItemKey, DateTime.SpecifyKind(request.OccurrenceStartUtc, DateTimeKind.Utc), cancellationToken);
        if (mark is null || mark.KurinKey != request.KurinKey)
        {
            return ScoreAccess.NotFound<object>("Nobody is marked here.");
        }

        var membership = (await _memberships.GetInKurinAsync(request.KurinKey, cancellationToken))
            .FirstOrDefault(m => m.MembershipKey == request.MembershipKey);
        if (!await _access.MayScoreAsync(request.KurinKey, membership?.LeftAtUtc is null ? membership?.GroupKey : null, ResourceAction.Delete, cancellationToken))
        {
            return ScoreAccess.Forbidden<object>();
        }

        var now = _time.GetUtcNow().UtcDateTime;
        mark.RemovedAtUtc = now;
        mark.RemovedByUserKey = _currentUser.UserId;
        mark.UpdatedDate = now;
        _score.ScoreAttendances.Update(mark, cancellationToken);
        ScoreTrail.Record(_score, request.KurinKey, ScoreTrail.Attendance, mark.ScoreAttendanceKey, ScoreTrail.Deleted,
            MarkAttendanceCommandHandler.Snapshot(mark), _currentUser.UserId, now);

        await _score.SaveChangesAsync(cancellationToken);
        return new ServiceResult<object>(ResultType.Success);
    }
}
