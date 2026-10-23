using MediatR;
using ProjectK.BusinessLogic.Modules.KurinModule.Services;
using ProjectK.Common.Interfaces;
using ProjectK.Common.Interfaces.Modules.InfrastructureModule;
using ProjectK.Common.Interfaces.Modules.MemberModule;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Records;

namespace ProjectK.BusinessLogic.Modules.KurinModule.Features.Agenda.Status;

/// <summary>
/// A drag on the board or a tick on the dashboard: moves what is the viewer's own on the task — their
/// part, or the shared targets they answer for (<see cref="AgendaCompletion.BoardMove"/>).
/// </summary>
public sealed record ChangeAgendaItemStatusCommand(Guid AgendaItemKey, AgendaItemStatus Status)
    : IRequest<ServiceResult<object>>;

public sealed class ChangeAgendaItemStatusCommandHandler : IRequestHandler<ChangeAgendaItemStatusCommand, ServiceResult<object>>
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

    public async Task<ServiceResult<object>> Handle(ChangeAgendaItemStatusCommand request, CancellationToken cancellationToken)
    {
        var item = await _uow.AgendaItems.GetByKeyWithAssignmentsAsync(request.AgendaItemKey, cancellationToken);
        if (item is null)
        {
            return ServiceResult<object>.Failure(ResultType.NotFound, "AGENDA_NOT_FOUND", "Agenda item was not found.");
        }

        var viewer = await _access.BuildViewerAsync(item.KurinKey, cancellationToken);
        if (viewer.KurinKey != _currentUser.KurinKey)
        {
            return ServiceResult<object>.Failure(ResultType.Forbidden, "AGENDA_OTHER_KURIN", "Agenda item belongs to a different kurin.");
        }

        var actor = viewer.ViewerUserKey ?? Guid.Empty;
        var before = item.Status;
        var now = _time.GetUtcNow().UtcDateTime;

        // An item without targets has no parts; its author and the whole kurin move it as one.
        if (item.Assignments.Count == 0)
        {
            if (!AgendaPermissions.CanManage(item, viewer))
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

        return new ServiceResult<object>(ResultType.Success);
    }

    private static ServiceResult<object> Forbidden() =>
        ServiceResult<object>.Failure(ResultType.Forbidden, "AGENDA_STATUS_FORBIDDEN", "Nothing on this task is yours to move.");
}
