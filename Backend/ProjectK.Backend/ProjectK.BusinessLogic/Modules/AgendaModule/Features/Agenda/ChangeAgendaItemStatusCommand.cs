using MediatR;
using ProjectK.BusinessLogic.Modules.AgendaModule.Services;
using ProjectK.Common.Contracts.AuditModule;
using ProjectK.Common.Contracts.InfrastructureModule;
using ProjectK.Common.Contracts.KurinModule;
using ProjectK.Common.Contracts.MemberModule;
using ProjectK.Common.Contracts.Shared;
using ProjectK.Common.Enums.AgendaModule;
using ProjectK.Common.Enums.Shared;
using ProjectK.Common.Records.Shared;
using ProjectK.Common.Rules.AgendaModule;

namespace ProjectK.BusinessLogic.Modules.AgendaModule.Features.Agenda;

/// <summary>
/// A drag on the board or a tick on the dashboard: moves what is the viewer's own on the task — their
/// part, or the shared targets they answer for (<see cref="AgendaCompletionRules.BoardMove"/>).
/// </summary>
public sealed record ChangeAgendaItemStatusCommand(Guid AgendaItemKey, AgendaItemStatus Status)
    : IRequest<ServiceResult<Unit>>;

public sealed class ChangeAgendaItemStatusCommandHandler : IRequestHandler<ChangeAgendaItemStatusCommand, ServiceResult<Unit>>
{
    private readonly IUnitOfWork _uow;
    private readonly IMemberDirectory _members;
    private readonly IOfficeDirectory _offices;
    private readonly IKurinTagDirectory _tags;
    private readonly IAgendaAccess _access;
    private readonly ICurrentUserContext _currentUser;
    private readonly IDomainEventPublisher _events;
    private readonly TimeProvider _time;
    private readonly IKurinAudit _audit;
    private readonly AgendaStatusWriter _statuses;

    public ChangeAgendaItemStatusCommandHandler(
        IUnitOfWork uow,
        IMemberDirectory members,
        IOfficeDirectory offices,
        IKurinTagDirectory tags,
        IAgendaAccess access,
        ICurrentUserContext currentUser,
        IDomainEventPublisher events,
        TimeProvider time,
        IKurinAudit audit,
        AgendaStatusWriter statuses)
    {
        _uow = uow;
        _members = members;
        _offices = offices;
        _tags = tags;
        _access = access;
        _currentUser = currentUser;
        _events = events;
        _time = time;
        _audit = audit;
        _statuses = statuses;
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

            AgendaCompletionRules.SetItemStatus(item, request.Status, now);
            item.UpdatedDate = now;
            await _statuses.RecordAsync(item, item.Title, null, null, request.Status, before, cancellationToken);
        }
        else
        {
            var moves = AgendaCompletionRules.BoardMove(item, viewer);
            if (moves.Count == 0)
            {
                return Forbidden();
            }

            var movedFrom = AgendaCompletionRules.StatusOf(moves[0]);
            var roster = await AgendaRosterRules.LoadAsync(_members, _offices, _tags, item.KurinKey, [item], cancellationToken);
            foreach (var stake in moves)
            {
                _statuses.Apply(item, stake, request.Status, actor, roster, now);
            }

            var whosePart = moves.Count == 1 ? moves[0].MemberKey : null;
            await _statuses.RecordAsync(item, item.Title, whosePart, movedFrom, request.Status, before, cancellationToken);
        }

        // The item is tracked (loaded with assignments), so the change is persisted without an explicit
        // Update() — which would re-mark the client-keyed assignments and fight change tracking.
        await _uow.SaveChangesAsync(cancellationToken);

        if (item.Status != before)
        {
            await _statuses.PublishAsync(item, actor, cancellationToken);
        }

        return new ServiceResult<Unit>(ResultType.Success, Unit.Value);
    }

    private static ServiceResult<Unit> Forbidden() =>
        ServiceResult<Unit>.Failure(ResultType.Forbidden, AgendaErrors.AgendaStatusForbidden, "Nothing on this task is yours to move.");
}
