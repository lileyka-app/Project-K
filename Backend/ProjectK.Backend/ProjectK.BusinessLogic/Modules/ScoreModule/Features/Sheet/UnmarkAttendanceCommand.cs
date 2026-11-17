using MediatR;
using ProjectK.BusinessLogic.Modules.ScoreModule.Services;
using ProjectK.Common.Contracts.KurinModule;
using ProjectK.Common.Contracts.NotificationsModule;
using ProjectK.Common.Contracts.ScoreModule;
using ProjectK.Common.Enums.IdentityModule;
using ProjectK.Common.Enums.Shared;
using ProjectK.Common.Records.Shared;

namespace ProjectK.BusinessLogic.Modules.ScoreModule.Features.Sheet;

/// <summary>Takes a mark off. Anyone who may score the person's гурток may, whoever put it there.</summary>
public sealed record UnmarkAttendanceCommand(Guid KurinKey, Guid AgendaItemKey, DateTime OccurrenceStartUtc, Guid MembershipKey)
    : IRequest<ServiceResult<Unit>>;

public sealed class UnmarkAttendanceCommandHandler : IRequestHandler<UnmarkAttendanceCommand, ServiceResult<Unit>>
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

    public async Task<ServiceResult<Unit>> Handle(UnmarkAttendanceCommand request, CancellationToken cancellationToken)
    {
        if (_access.Refuse<Unit>(request.KurinKey) is { } refused)
        {
            return refused;
        }

        var mark = await _score.ScoreAttendances.GetStandingAsync(
            request.MembershipKey, request.AgendaItemKey, DateTime.SpecifyKind(request.OccurrenceStartUtc, DateTimeKind.Utc), cancellationToken);
        if (mark is null || mark.KurinKey != request.KurinKey)
        {
            return ScoreErrors.NotFound<Unit>("Nobody is marked here.");
        }

        var membership = (await _memberships.GetInKurinAsync(request.KurinKey, cancellationToken))
            .FirstOrDefault(m => m.MembershipKey == request.MembershipKey);
        if (!await _access.MayScoreAsync(request.KurinKey, membership?.LeftAtUtc is null ? membership?.GroupKey : null, ResourceAction.Delete, cancellationToken))
        {
            return ScoreErrors.Refused<Unit>();
        }

        var now = _time.GetUtcNow().UtcDateTime;
        mark.RemovedAtUtc = now;
        mark.RemovedByUserKey = _currentUser.UserId;
        mark.UpdatedDate = now;
        _score.ScoreAttendances.Update(mark, cancellationToken);
        ScoreTrail.Record(_score, request.KurinKey, ScoreTrail.Attendance, mark.ScoreAttendanceKey, ScoreTrail.Deleted,
            MarkAttendanceCommandHandler.Snapshot(mark), _currentUser.UserId, now);

        await _score.SaveChangesAsync(cancellationToken);
        return new ServiceResult<Unit>(ResultType.Success, Unit.Value);
    }
}
