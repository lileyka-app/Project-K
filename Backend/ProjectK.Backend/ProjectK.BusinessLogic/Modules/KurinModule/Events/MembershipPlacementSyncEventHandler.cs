using MediatR;
using ProjectK.BusinessLogic.Services.Events;
using ProjectK.Common.Contracts.Shared;
using ProjectK.Common.Models.Events;

namespace ProjectK.BusinessLogic.Modules.KurinModule.Events;

/// <summary>
/// Makes the kurin's own record of who belongs to it follow what a person's record says about where
/// they are placed. Lists, гуртки and access decisions all read the membership now, so a placement
/// that never reached this table would leave someone written into a kurin and invisible in it.
/// <para>
/// A shim for as long as both say it. Once a person is put in a kurin by joining it, this is what
/// the join itself writes and there is nothing to mirror.
/// </para>
/// </summary>
public sealed class MembershipPlacementSyncEventHandler : INotificationHandler<DomainEventNotification<MemberPlaced>>
{
    private readonly IUnitOfWork _unitOfWork;

    public MembershipPlacementSyncEventHandler(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public Task Handle(DomainEventNotification<MemberPlaced> notification, CancellationToken cancellationToken)
        => _unitOfWork.Memberships.PlaceAsync(
            notification.Event.MemberKey,
            notification.Event.UserKey,
            notification.Event.KurinKey,
            notification.Event.GroupKey,
            cancellationToken);
}
