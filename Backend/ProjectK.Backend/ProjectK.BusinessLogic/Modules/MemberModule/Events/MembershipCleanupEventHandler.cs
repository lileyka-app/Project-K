using MediatR;
using ProjectK.BusinessLogic.Services.Events;
using ProjectK.Common.Contracts.Shared;
using ProjectK.Common.Events.KurinModule;
using ProjectK.Common.Events.MemberModule;

namespace ProjectK.BusinessLogic.Modules.MemberModule.Events;

/// <summary>
/// Closes the books on memberships and offices whose other end is gone: a person removed, a kurin
/// deleted, a гурток dissolved. A membership names its person by a bare key — that is what keeps the
/// two tables independent — so nothing in the database follows them out, and the rows left behind
/// would go on placing someone who no longer exists. Every change stays on the request's unit of
/// work and commits with the save of whoever announced the event.
/// </summary>
public sealed class MembershipCleanupEventHandler :
    INotificationHandler<DomainEventNotification<MembersRemoved>>,
    INotificationHandler<DomainEventNotification<KurinDeleted>>,
    INotificationHandler<DomainEventNotification<GroupDissolved>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDomainEventPublisher _events;

    public MembershipCleanupEventHandler(IUnitOfWork unitOfWork, IDomainEventPublisher events)
    {
        _unitOfWork = unitOfWork;
        _events = events;
    }

    /// <summary>
    /// The people's memberships go; once they are dropped, <see cref="MembershipsRemoved"/> tells the
    /// modules that key by membership — the вкладка — to do the same.
    /// </summary>
    public async Task Handle(DomainEventNotification<MembersRemoved> notification, CancellationToken cancellationToken)
    {
        var membershipKeys = await _unitOfWork.Memberships.RemoveForMembersAsync(notification.Event.MemberKeys, cancellationToken);
        if (membershipKeys.Count > 0)
        {
            await _events.PublishAsync(new MembershipsRemoved(membershipKeys), cancellationToken);
        }
    }

    /// <summary>
    /// The people stay; what they earned stays with them. Offices are NO ACTION against both the kurin
    /// and its гуртки, so they go first, and the memberships name the kurin by a foreign key, so they go
    /// with it.
    /// </summary>
    public async Task Handle(DomainEventNotification<KurinDeleted> notification, CancellationToken cancellationToken)
    {
        var kurinKey = notification.Event.KurinKey;
        await _unitOfWork.Leaderships.DeleteForKurinAsync(kurinKey, cancellationToken);
        await _unitOfWork.Memberships.RemoveForKurinAsync(kurinKey, cancellationToken);
    }

    /// <summary>
    /// The гурток's проводи close, its впорядники lead nothing (the revoked rows stay as history), and
    /// its people stay in the kurin without a гурток, which a membership is allowed to be.
    /// </summary>
    public async Task Handle(DomainEventNotification<GroupDissolved> notification, CancellationToken cancellationToken)
    {
        var (_, groupKey, dissolvedAtUtc) = notification.Event;
        await _unitOfWork.Leaderships.CloseForGroupAsync(groupKey, DateOnly.FromDateTime(dissolvedAtUtc), cancellationToken);

        foreach (var assignment in await _unitOfWork.MentorAssignments.GetByGroupKeyAsync(groupKey, cancellationToken))
        {
            assignment.RevokedAtUtc ??= dissolvedAtUtc;
        }

        await _unitOfWork.Memberships.DetachFromGroupAsync(groupKey, cancellationToken);
    }
}
