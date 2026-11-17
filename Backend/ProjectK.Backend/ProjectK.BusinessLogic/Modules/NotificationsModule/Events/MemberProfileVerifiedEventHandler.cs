using MediatR;
using ProjectK.BusinessLogic.Services.Events;
using ProjectK.Common.Contracts.NotificationsModule;
using ProjectK.Common.Dtos.NotificationsModule;
using ProjectK.Common.Enums.NotificationsModule;
using ProjectK.Common.Events.MemberModule;

namespace ProjectK.BusinessLogic.Modules.NotificationsModule.Events;

public sealed class MemberProfileVerifiedEventHandler
    : INotificationHandler<DomainEventNotification<MemberProfileVerified>>
{
    private readonly INotificationService _notifications;

    public MemberProfileVerifiedEventHandler(INotificationService notifications)
    {
        _notifications = notifications;
    }

    public Task Handle(DomainEventNotification<MemberProfileVerified> notification, CancellationToken cancellationToken)
    {
        var raised = notification.Event;
        return _notifications.NotifyAsync(
            new NotificationRequest
            {
                RecipientUserKey = raised.MemberUserKey,
                Type = AppNotificationType.MemberProfileVerified,
                Severity = AppNotificationSeverity.Success,
                Title = "Профільні дані підтверджено",
                Body = "Ваші профільні дані підтверджено як актуальні.",
                EntityType = "Member",
                EntityKey = raised.MemberKey,
                Route = $"/member/{raised.MemberKey}",
                ActorUserKey = raised.ActorUserKey,
                DeduplicationKey = $"member-profile-verified:{raised.MemberKey}"
            },
            cancellationToken);
    }
}
