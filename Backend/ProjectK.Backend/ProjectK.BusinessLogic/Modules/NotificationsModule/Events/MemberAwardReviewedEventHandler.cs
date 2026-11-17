using MediatR;
using ProjectK.BusinessLogic.Services.Events;
using ProjectK.Common.Contracts.NotificationsModule;
using ProjectK.Common.Dtos.NotificationsModule;
using ProjectK.Common.Enums.NotificationsModule;
using ProjectK.Common.Events.MemberModule;

namespace ProjectK.BusinessLogic.Modules.NotificationsModule.Events;

public sealed class MemberAwardReviewedEventHandler
    : INotificationHandler<DomainEventNotification<MemberAwardReviewed>>
{
    private readonly INotificationService _notifications;

    public MemberAwardReviewedEventHandler(INotificationService notifications)
    {
        _notifications = notifications;
    }

    public Task Handle(DomainEventNotification<MemberAwardReviewed> notification, CancellationToken cancellationToken)
    {
        var raised = notification.Event;
        return _notifications.NotifyAsync(
            new NotificationRequest
            {
                RecipientUserKey = raised.MemberUserKey,
                Type = AppNotificationType.MemberAwardReviewed,
                Severity = raised.IsApproved ? AppNotificationSeverity.Success : AppNotificationSeverity.Warn,
                Title = raised.IsApproved ? "Відзначення затверджено" : "Відзначення не затверджено",
                Body = raised.IsApproved
                    ? "Ваше відзначення затверджено."
                    : "Ваше відзначення не затверджено. Перегляньте зауваження.",
                EntityType = "MemberAward",
                EntityKey = raised.MemberAwardKey,
                Route = $"/member/{raised.MemberKey}",
                ActorUserKey = raised.ActorUserKey,
                DeduplicationKey = $"award-review:{raised.MemberAwardKey}"
            },
            cancellationToken);
    }
}
