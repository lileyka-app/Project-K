using MediatR;
using ProjectK.BusinessLogic.Modules.KurinModule.Services;
using ProjectK.Common.Contracts.AuditModule;
using ProjectK.Common.Contracts.InfrastructureModule;
using ProjectK.Common.Contracts.MemberModule;
using ProjectK.Common.Contracts.Shared;
using ProjectK.Common.Enums.KurinModule;
using ProjectK.Common.Enums.Shared;
using ProjectK.Common.Records.Shared;

namespace ProjectK.BusinessLogic.Modules.KurinModule.Features.Agenda;

/// <summary>
/// A drag on the board or a tick on the dashboard: moves what is the viewer's own on the task — their
/// part, or the shared targets they answer for (<see cref="AgendaCompletion.BoardMove"/>).
/// </summary>
public sealed record ChangeAgendaItemStatusCommand(Guid AgendaItemKey, AgendaItemStatus Status)
    : IRequest<ServiceResult<Unit>>;

public sealed class ChangeAgendaItemStatusCommandHandler : IRequestHandler<ChangeAgendaItemStatusCommand, ServiceResult<Unit>>
{
    private readonly IUnitOfWork _uow;
    private readonly IMemberDirectory _members;
    private readonly IAgendaAccess _access;
    private readonly ICurrentUserContext _currentUser;
    private readonly IDomainEventPublisher _events;
    private readonly TimeProvider _time;
    private readonly IKurinAudit _audit;

    public ChangeAgendaItemStatusCommandHandler(
        IUnitOfWork uow,
        IMemberDirectory members,
        IAgendaAccess access,
        ICurrentUserContext currentUser,
        IDomainEventPublisher events,
        TimeProvider time,
        IKurinAudit audit)
    {
        _uow = uow;
        _members = members;
        _access = access;
        _currentUser = currentUser;
        _events = events;
        _time = time;
        _audit = audit;
    }

    public async Task<ServiceResult<Unit>> Handle(ChangeAgendaItemStatusCommand request, CancellationToken cancellationToken)
    {
        var item = await _uow.AgendaItems.GetByKeyWithAssignmentsAsync(request.AgendaItemKey, cancellationToken);
        if (item is null)
        {
            return AgendaErrors.NotFound<Unit>();
        }

        var viewer = await _access.BuildViewerAsync(item.KurinKey, cancellationToken);
        if (viewer.KurinKey != _currentUser.KurinKey)
        {
            return AgendaErrors.OtherKurin<Unit>();
        }

        var actor = viewer.ViewerUserKey ?? Guid.Empty;
        var before = item.Status;
        var now = _time.GetUtcNow().UtcDateTime;

        // An item without targets has no parts; its author and the whole kurin move it as one.
        if (item.Assignments.Count == 0)
        {
            if (!AgendaAccess.CanManage(item, viewer))
            {
                return Forbidden();
            }

            AgendaCompletion.SetItemStatus(item, request.Status, now);
            item.UpdatedDate = now;
            await AgendaStatusWriter.RecordAsync(_audit, item, item.Title, null, null, request.Status, before, cancellationToken);
        }
        else
        {
            var moves = AgendaCompletion.BoardMove(item, viewer);
            if (moves.Count == 0)
            {
                return Forbidden();
            }

            var movedFrom = AgendaCompletion.StatusOf(moves[0]);
            var roster = await AgendaRoster.LoadAsync(_uow, _members, item.KurinKey, [item], cancellationToken);
            foreach (var stake in moves)
            {
                AgendaStatusWriter.Apply(_uow, item, stake, request.Status, actor, roster, now);
            }

            var whosePart = moves.Count == 1 ? moves[0].MemberKey : null;
            await AgendaStatusWriter.RecordAsync(_audit, item, item.Title, whosePart, movedFrom, request.Status, before, cancellationToken);
        }

        // The item is tracked (loaded with assignments), so the change is persisted without an explicit
        // Update() — which would re-mark the client-keyed assignments and fight change tracking.
        await _uow.SaveChangesAsync(cancellationToken);

        if (item.Status != before)
        {
            await AgendaStatusWriter.PublishAsync(_events, item, actor, cancellationToken);
        }

        return new ServiceResult<Unit>(ResultType.Success, Unit.Value);
    }

    private static ServiceResult<Unit> Forbidden() =>
        ServiceResult<Unit>.Failure(ResultType.Forbidden, AgendaErrors.AgendaStatusForbidden, "Nothing on this task is yours to move.");
}
