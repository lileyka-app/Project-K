using MediatR;
using ProjectK.BusinessLogic.Services.Events;
using ProjectK.Common.Contracts.Shared;
using ProjectK.Common.Events.MemberModule;

namespace ProjectK.BusinessLogic.Modules.MemberModule.Events;

/// <summary>
/// Closes the kurin's books on people who are gone. A membership names its person by a bare key —
/// that is what keeps the two tables independent — so nothing in the database follows them out, and
/// the rows left behind would go on placing someone who no longer exists. Once they are dropped,
/// <see cref="MembershipsRemoved"/> tells the modules that key by membership — the вкладка — to do the same.
/// </summary>
public sealed class MembershipCleanupEventHandler : INotificationHandler<DomainEventNotification<MembersRemoved>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDomainEventPublisher _events;

    public MembershipCleanupEventHandler(IUnitOfWork unitOfWork, IDomainEventPublisher events)
    {
        _unitOfWork = unitOfWork;
        _events = events;
    }

    public async Task Handle(DomainEventNotification<MembersRemoved> notification, CancellationToken cancellationToken)
    {
        var membershipKeys = await _unitOfWork.Memberships.RemoveForMembersAsync(notification.Event.MemberKeys, cancellationToken);
        if (membershipKeys.Count > 0)
        {
            await _events.PublishAsync(new MembershipsRemoved(membershipKeys), cancellationToken);
        }
    }
}
