using FluentValidation;
using MediatR;
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

namespace ProjectK.BusinessLogic.Modules.ScoreModule.Features.Sheet;

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
