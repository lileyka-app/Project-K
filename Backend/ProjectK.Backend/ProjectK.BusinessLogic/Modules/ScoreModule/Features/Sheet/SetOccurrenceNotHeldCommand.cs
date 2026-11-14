using MediatR;
using ProjectK.BusinessLogic.Modules.ScoreModule.Services;
using ProjectK.Common.Contracts.AuditModule;
using ProjectK.Common.Contracts.InfrastructureModule;
using ProjectK.Common.Contracts.KraiModule;
using ProjectK.Common.Contracts.ScoreModule;
using ProjectK.Common.Contracts.Shared;
using ProjectK.Common.Entities.ScoreModule;
using ProjectK.Common.Enums.AuditModule;
using ProjectK.Common.Enums.Shared;
using ProjectK.Common.Records.AuditModule;
using ProjectK.Common.Records.Shared;
using ProjectK.Common.Rules.AuditModule;

namespace ProjectK.BusinessLogic.Modules.ScoreModule.Features.Sheet;

/// <summary>
/// Says one occurrence of an event did not take place — or takes that back. It drops the occurrence
/// from the провід's «Справи», where it would otherwise wait for a sheet nobody will fill. Whoever may
/// open the sheet may do either; asking for what already stands changes nothing.
/// </summary>
public sealed record SetOccurrenceNotHeldCommand(Guid KurinKey, Guid AgendaItemKey, DateTime OccurrenceStartUtc, bool NotHeld)
    : IRequest<ServiceResult<Unit>>;

public sealed class SetOccurrenceNotHeldCommandHandler : IRequestHandler<SetOccurrenceNotHeldCommand, ServiceResult<Unit>>
{
    private readonly ScoreAccess _access;
    private readonly IScoreUnitOfWork _score;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserContext _currentUser;
    private readonly TimeProvider _time;
    private readonly IKraiProfileDirectory _profiles;
    private readonly IKurinAudit _audit;

    public SetOccurrenceNotHeldCommandHandler(
        ScoreAccess access,
        IScoreUnitOfWork score,
        IUnitOfWork unitOfWork,
        ICurrentUserContext currentUser,
        TimeProvider time,
        IKraiProfileDirectory profiles,
        IKurinAudit audit)
    {
        _access = access;
        _score = score;
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
        _time = time;
        _profiles = profiles;
        _audit = audit;
    }

    public async Task<ServiceResult<Unit>> Handle(SetOccurrenceNotHeldCommand request, CancellationToken cancellationToken)
    {
        if (_access.Refuse<Unit>(request.KurinKey) is { } refused)
        {
            return refused;
        }

        var occurrence = await EventOccurrence.LoadAsync(_unitOfWork, _profiles, request.KurinKey, request.AgendaItemKey, request.OccurrenceStartUtc, cancellationToken);
        if (occurrence is null)
        {
            return ScoreErrors.NotFound<Unit>("There is no such event, or it does not meet that day.");
        }

        var groupKeys = (await _unitOfWork.Groups.GetAllAsync(request.KurinKey, cancellationToken)).Select(g => g.GroupKey);
        if (!await _access.MayMarkAnyoneAsync(request.KurinKey, groupKeys, cancellationToken))
        {
            return ScoreErrors.Refused<Unit>();
        }

        var standing = await _score.ScoreNotHeldOccurrences.FindAsync(request.AgendaItemKey, occurrence.StartUtc, cancellationToken);
        if (request.NotHeld == standing is not null)
        {
            return new ServiceResult<Unit>(ResultType.Success, Unit.Value);
        }

        if (request.NotHeld)
        {
            if (await _score.ScoreAttendances.AnyAtOccurrenceAsync(request.AgendaItemKey, occurrence.StartUtc, cancellationToken))
            {
                return ServiceResult<Unit>.Failure(ResultType.Conflict, ScoreErrors.OccurrenceHasAttendance, "Someone is marked present at this occurrence, so it took place.");
            }

            var now = _time.GetUtcNow().UtcDateTime;
            _score.ScoreNotHeldOccurrences.Create(new ScoreNotHeldOccurrence
            {
                KurinKey = request.KurinKey,
                AgendaItemKey = request.AgendaItemKey,
                OccurrenceStartUtc = occurrence.StartUtc,
                MarkedByUserKey = _currentUser.UserId,
                MarkedAtUtc = now,
                CreatedDate = now,
                UpdatedDate = now
            }, cancellationToken);
        }
        else
        {
            _score.ScoreNotHeldOccurrences.Delete(standing!, cancellationToken);
        }

        await RecordAsync(_audit, request.KurinKey, occurrence, request.NotHeld, cancellationToken);
        try
        {
            await _score.SaveChangesAsync(cancellationToken);
        }
        catch (DuplicateRowException)
        {
            // Someone else said the same a moment earlier: what was asked for stands.
        }

        return new ServiceResult<Unit>(ResultType.Success, Unit.Value);
    }

    /// <summary>The journal entry for either way, also written when a mark takes «did not take place» back.</summary>
    internal static Task RecordAsync(IKurinAudit audit, Guid kurinKey, EventOccurrence occurrence, bool notHeld, CancellationToken cancellationToken) =>
        audit.RecordAsync(
            new AuditRecord(
                AuditArea.Agenda,
                notHeld ? AuditActionNames.Agenda.OccurrenceNotHeld : AuditActionNames.Agenda.OccurrenceHeld,
                occurrence.Item.AgendaItemKey,
                occurrence.Label,
                KurinKey: kurinKey),
            cancellationToken);
}
