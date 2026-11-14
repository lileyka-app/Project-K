using MediatR;
using ProjectK.BusinessLogic.Services.Events;
using ProjectK.Common.Contracts.MemberModule;
using ProjectK.Common.Events.UsersModule;

namespace ProjectK.BusinessLogic.Modules.MemberModule.Events;

/// <summary>
/// Carries a contact change from the account to the member linked to it, and commits it. Does nothing
/// when the account has no member. The account is the source of truth for contact details.
/// </summary>
public sealed class AccountContactSyncEventHandler :
    INotificationHandler<DomainEventNotification<AccountEmailChanged>>,
    INotificationHandler<DomainEventNotification<AccountPhoneChanged>>
{
    private readonly IMemberUnitOfWork _unitOfWork;

    public AccountContactSyncEventHandler(IMemberUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task Handle(DomainEventNotification<AccountEmailChanged> notification, CancellationToken cancellationToken)
    {
        var member = await _unitOfWork.Members.GetTrackedByUserKeyAsync(notification.Event.UserKey, cancellationToken);
        if (member is null)
        {
            return;
        }

        member.Email = notification.Event.Email;
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task Handle(DomainEventNotification<AccountPhoneChanged> notification, CancellationToken cancellationToken)
    {
        var member = await _unitOfWork.Members.GetTrackedByUserKeyAsync(notification.Event.UserKey, cancellationToken);
        if (member is null)
        {
            return;
        }

        member.PhoneNumber = notification.Event.PhoneNumber;
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
