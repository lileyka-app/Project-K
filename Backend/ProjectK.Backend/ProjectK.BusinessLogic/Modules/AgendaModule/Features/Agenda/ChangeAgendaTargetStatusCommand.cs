using MediatR;
using ProjectK.BusinessLogic.Modules.AgendaModule.Services;
using ProjectK.Common.Contracts.AuditModule;
using ProjectK.Common.Contracts.KurinModule;
using ProjectK.Common.Contracts.MemberModule;
using ProjectK.Common.Contracts.NotificationsModule;
using ProjectK.Common.Contracts.Shared;
using ProjectK.Common.Enums.AgendaModule;
using ProjectK.Common.Enums.Shared;
using ProjectK.Common.Records.AgendaModule;
using ProjectK.Common.Records.Shared;
using ProjectK.Common.Rules.AgendaModule;

namespace ProjectK.BusinessLogic.Modules.AgendaModule.Features.Agenda;

/// <summary>
/// Moves one named part from the task's dialog: a target's shared state, or — with
/// <see cref="MemberKey"/> — one person's part of a target done «кожному окремо», which is how a
/// гуртковий marks the youth who handed the вкладка over in cash.
/// </summary>
public sealed record ChangeAgendaTargetStatusCommand(Guid AgendaItemKey, Guid AgendaAssignmentKey, AgendaItemStatus Status, Guid? MemberKey)
    : IRequest<ServiceResult<Unit>>;

public sealed class ChangeAgendaTargetStatusCommandHandler : IRequestHandler<ChangeAgendaTargetStatusCommand, ServiceResult<Unit>>
{
    private readonly IUnitOfWork _uow;
    private readonly IMembershipDirectory _memberships;
    private readonly IOfficeDirectory _offices;
    private readonly IKurinDirectory _kurins;
    private readonly IKurinTagDirectory _tags;
    private readonly IAgendaAccess _access;
    private readonly ICurrentUserContext _currentUser;
    private readonly IDomainEventPublisher _events;
    private readonly TimeProvider _time;
    private readonly IKurinAudit _audit;
    private readonly AgendaStatusWriter _statuses;

    public ChangeAgendaTargetStatusCommandHandler(
        IUnitOfWork uow,
        IMembershipDirectory memberships,
        IOfficeDirectory offices,
        IKurinDirectory kurins,
        IKurinTagDirectory tags,
        IAgendaAccess access,
        ICurrentUserContext currentUser,
        IDomainEventPublisher events,
        TimeProvider time,
        IKurinAudit audit,
        AgendaStatusWriter statuses)
    {
        _uow = uow;
        _memberships = memberships;
        _offices = offices;
        _kurins = kurins;
        _tags = tags;
        _access = access;
        _currentUser = currentUser;
        _events = events;
        _time = time;
        _audit = audit;
        _statuses = statuses;
    }

    public async Task<ServiceResult<Unit>> Handle(ChangeAgendaTargetStatusCommand request, CancellationToken cancellationToken)
    {
        var item = await _uow.AgendaItems.GetByKeyWithAssignmentsAsync(request.AgendaItemKey, cancellationToken);
        var assignment = item?.Assignments.FirstOrDefault(a => a.AgendaAssignmentKey == request.AgendaAssignmentKey);
        if (item is null || assignment is null)
        {
            return ServiceResult<Unit>.Failure(ResultType.NotFound, AgendaErrors.AgendaNotFound, "Agenda item or target was not found.");
        }

        var viewer = await _access.BuildViewerAsync(item.KurinKey, cancellationToken);
        if (viewer.KurinKey != _currentUser.KurinKey)
        {
            return AgendaErrors.OtherKurin<Unit>();
        }

        var perMember = AgendaCompletionRules.ModeOf(assignment) == AgendaCompletionMode.PerMember;
        if (perMember != request.MemberKey.HasValue)
        {
            return ServiceResult<Unit>.Failure(ResultType.BadRequest, AgendaErrors.AgendaPartMismatch,
                perMember ? "This target is done by each person; name whose part moves." : "This target has one state; no person is named.");
        }

        var roster = await AgendaRosterRules.LoadAsync(_memberships, _offices, _tags, item.KurinKey, [item], cancellationToken);
        if (request.MemberKey is { } memberKey)
        {
            if (!roster.PeopleIn(assignment).Contains(memberKey))
            {
                return ServiceResult<Unit>.Failure(ResultType.BadRequest, AgendaErrors.AgendaNotInTarget, "That person is not in this target.");
            }

            if (!AgendaCompletionRules.CanMovePart(item, assignment, viewer, memberKey))
            {
                return Forbidden();
            }
        }
        else if (!AgendaCompletionRules.CanMoveTarget(item, assignment, viewer))
        {
            return Forbidden();
        }

        var before = item.Status;
        var actor = viewer.ViewerUserKey ?? Guid.Empty;
        var stake = new AgendaStake(assignment, request.MemberKey);
        var movedFrom = AgendaCompletionRules.StatusOf(stake);
        _statuses.Apply(item, stake, request.Status, actor, roster, _time.GetUtcNow().UtcDateTime);

        // A person's part names the person; a shared target is named in the label instead.
        var label = item.Title;
        if (request.MemberKey is null)
        {
            (AgendaTargetType, Guid)[] target = [(assignment.TargetType, assignment.TargetKey)];
            var names = await AgendaTargetNames.LoadAsync(_memberships, _offices, _kurins, _tags, item.KurinKey, target, cancellationToken);
            label = $"{item.Title} — {names.Describe(target)}";
        }

        await _statuses.RecordAsync(item, label, request.MemberKey, movedFrom, request.Status, before, cancellationToken);
        await _uow.SaveChangesAsync(cancellationToken);

        if (item.Status != before)
        {
            await _statuses.PublishAsync(item, actor, cancellationToken);
        }

        return new ServiceResult<Unit>(ResultType.Success, Unit.Value);
    }

    private static ServiceResult<Unit> Forbidden() =>
        ServiceResult<Unit>.Failure(ResultType.Forbidden, AgendaErrors.AgendaStatusForbidden, "This part of the task is not yours to move.");
}
