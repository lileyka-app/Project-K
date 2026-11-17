using MediatR;
using ProjectK.BusinessLogic.Services.Events;
using ProjectK.Common.Contracts.KurinModule;
using ProjectK.Common.Contracts.Shared;
using ProjectK.Common.Events.KurinModule;
using ProjectK.Common.Events.MemberModule;

namespace ProjectK.BusinessLogic.Modules.AgendaModule.Events;

/// <summary>
/// Drops the assignments of tasks and events whose target is gone: a person removed, a гурток dissolved
/// with its проводи, a tag deleted. An assignment names its target by a bare key across four tables, so
/// nothing in the database follows them out; left behind, it reaches nobody. Every change stays on the
/// request's unit of work and commits with the save of whoever announced the event.
/// </summary>
public sealed class AgendaTargetCleanupEventHandler :
    INotificationHandler<DomainEventNotification<MembersRemoved>>,
    INotificationHandler<DomainEventNotification<GroupDissolved>>,
    INotificationHandler<DomainEventNotification<KurinTagRemoved>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IOfficeDirectory _offices;

    public AgendaTargetCleanupEventHandler(IUnitOfWork unitOfWork, IOfficeDirectory offices)
    {
        _unitOfWork = unitOfWork;
        _offices = offices;
    }

    public Task Handle(DomainEventNotification<MembersRemoved> notification, CancellationToken cancellationToken)
        => _unitOfWork.AgendaItems.RemoveAssignmentsForTargetsAsync(notification.Event.MemberKeys, cancellationToken);

    /// <summary>Neither the гурток nor its проводи can be a target any more.</summary>
    public async Task Handle(DomainEventNotification<GroupDissolved> notification, CancellationToken cancellationToken)
    {
        var (kurinKey, groupKey, _) = notification.Event;
        var leadershipKeys = (await _offices.GetLeadershipsInKurinAsync(kurinKey, cancellationToken))
            .Where(leadership => leadership.GroupKey == groupKey)
            .Select(leadership => leadership.LeadershipKey);

        await _unitOfWork.AgendaItems.RemoveAssignmentsForTargetsAsync([groupKey, .. leadershipKeys], cancellationToken);
    }

    public Task Handle(DomainEventNotification<KurinTagRemoved> notification, CancellationToken cancellationToken)
        => _unitOfWork.AgendaItems.RemoveAssignmentsForTargetsAsync([notification.Event.KurinTagKey], cancellationToken);
}
