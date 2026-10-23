using MediatR;
using ProjectK.BusinessLogic.Modules.ScoreModule.Services;
using ProjectK.Common.Entities.ScoreModule;
using ProjectK.Common.Exceptions;
using ProjectK.Common.Interfaces;
using ProjectK.Common.Interfaces.Modules.InfrastructureModule;
using ProjectK.Common.Interfaces.Modules.KraiModule;
using ProjectK.Common.Interfaces.Modules.ScoreModule;
using ProjectK.Common.Models.Audit;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Records;

namespace ProjectK.BusinessLogic.Modules.ScoreModule.Features.Sheet.NotHeld;

/// <summary>
/// Says one occurrence of an event did not take place — or takes that back. It drops the occurrence
/// from the провід's «Справи», where it would otherwise wait for a sheet nobody will fill. Whoever may
/// open the sheet may do either; asking for what already stands changes nothing.
/// </summary>
public sealed record SetOccurrenceNotHeldCommand(Guid KurinKey, Guid AgendaItemKey, DateTime OccurrenceStartUtc, bool NotHeld)
    : IRequest<ServiceResult<object>>;

public sealed class SetOccurrenceNotHeldCommandHandler : IRequestHandler<SetOccurrenceNotHeldCommand, ServiceResult<object>>
{
    private readonly ScoreAccess _access;
    private readonly IScoreUnitOfWork _score;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserContext _currentUser;
    private readonly TimeProvider _time;
    private readonly IKraiProfileProvider _profiles;
    private readonly IKurinAudit _audit;

    public SetOccurrenceNotHeldCommandHandler(
        ScoreAccess access,
        IScoreUnitOfWork score,
        IUnitOfWork unitOfWork,
        ICurrentUserContext currentUser,
        TimeProvider time,
        IKraiProfileProvider profiles,
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

    public async Task<ServiceResult<object>> Handle(SetOccurrenceNotHeldCommand request, CancellationToken cancellationToken)
    {
        if (_access.Refuse<object>(request.KurinKey) is { } refused)
        {
            return refused;
        }

        var occurrence = await EventOccurrence.LoadAsync(_unitOfWork, _profiles, request.KurinKey, request.AgendaItemKey, request.OccurrenceStartUtc, cancellationToken);
        if (occurrence is null)
        {
            return ScoreAccess.NotFound<object>("There is no such event, or it does not meet that day.");
        }

        var groupKeys = (await _unitOfWork.Groups.GetAllAsync(request.KurinKey, cancellationToken)).Select(g => g.GroupKey);
        if (!await _access.MayMarkAnyoneAsync(request.KurinKey, groupKeys, cancellationToken))
        {
            return ScoreAccess.Forbidden<object>();
        }

        var standing = await _score.ScoreNotHeldOccurrences.FindAsync(request.AgendaItemKey, occurrence.StartUtc, cancellationToken);
        if (request.NotHeld == standing is not null)
        {
            return new ServiceResult<object>(ResultType.Success);
        }

        if (request.NotHeld)
        {
            if (await _score.ScoreAttendances.AnyAtOccurrenceAsync(request.AgendaItemKey, occurrence.StartUtc, cancellationToken))
            {
                return ServiceResult<object>.Failure(ResultType.Conflict, "OccurrenceHasAttendance", "Someone is marked present at this occurrence, so it took place.");
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

        return new ServiceResult<object>(ResultType.Success);
    }

    /// <summary>The journal entry for either way, also written when a mark takes «did not take place» back.</summary>
    internal static Task RecordAsync(IKurinAudit audit, Guid kurinKey, EventOccurrence occurrence, bool notHeld, CancellationToken cancellationToken) =>
        audit.RecordAsync(
            new AuditRecord(
                AuditArea.Agenda,
                notHeld ? AuditActions.Agenda.OccurrenceNotHeld : AuditActions.Agenda.OccurrenceHeld,
                occurrence.Item.AgendaItemKey,
                occurrence.Label,
                KurinKey: kurinKey),
            cancellationToken);
}
