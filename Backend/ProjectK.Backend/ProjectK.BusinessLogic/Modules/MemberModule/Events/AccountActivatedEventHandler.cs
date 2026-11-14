using MediatR;
using ProjectK.BusinessLogic.Services.Events;
using ProjectK.Common.Contracts.MemberModule;
using ProjectK.Common.Contracts.Shared;
using ProjectK.Common.Entities.MemberModule;
using ProjectK.Common.Events.AuthModule;
using ProjectK.Common.Events.MemberModule;

namespace ProjectK.BusinessLogic.Modules.MemberModule.Events;

/// <summary>
/// Makes sure an activated account has a member: links the existing record when one already carries
/// that address, otherwise opens a new one from what the account knows. The write is left on the
/// request's unit of work, so it commits with the rest of the activation rather than half of it.
/// </summary>
public sealed class AccountActivatedEventHandler : INotificationHandler<DomainEventNotification<AccountActivated>>
{
    private readonly IMemberUnitOfWork _unitOfWork;
    private readonly IDomainEventPublisher _events;

    public AccountActivatedEventHandler(IMemberUnitOfWork unitOfWork, IDomainEventPublisher events)
    {
        _unitOfWork = unitOfWork;
        _events = events;
    }

    public async Task Handle(DomainEventNotification<AccountActivated> notification, CancellationToken cancellationToken)
    {
        var details = notification.Event.Account;

        var existing = await _unitOfWork.Members.GetByEmailAsync(details.Email, cancellationToken);
        if (existing is not null)
        {
            existing.UserKey = details.UserKey;
            _unitOfWork.Members.Update(existing, cancellationToken);
            await _events.PublishAsync(
                new MemberAccountLinked(existing.MemberKey, details.UserKey),
                cancellationToken);
            return;
        }

        var member = new Member
        {
            MemberKey = Guid.NewGuid(),
            FirstName = details.FirstName,
            LastName = details.LastName,
            Email = details.Email,
            PhoneNumber = details.PhoneNumber,
            DateOfBirth = details.DateOfBirth,
            UserKey = details.UserKey
        };

        _unitOfWork.Members.Create(member, cancellationToken);
        await _events.PublishAsync(
            new MemberAccountLinked(member.MemberKey, details.UserKey),
            cancellationToken);

        // An account can arrive without a kurin behind it, and then there is no membership to open —
        // the member record cannot name a kurin either, and that is where it fails.
        if (details.KurinKey != Guid.Empty)
        {
            await _events.PublishAsync(
                new MemberPlaced(member.MemberKey, details.UserKey, details.KurinKey, null),
                cancellationToken);
        }
    }
}
