using MediatR;
using ProjectK.BusinessLogic.Services.Events;
using ProjectK.Common.Contracts.NotificationsModule;
using ProjectK.Common.Dtos.NotificationsModule;
using ProjectK.Common.Enums.NotificationsModule;
using ProjectK.Common.Events.ProbesAndBadgesModule;

namespace ProjectK.BusinessLogic.Modules.NotificationsModule.Events;

public sealed class BadgeProgressReviewedEventHandler
    : INotificationHandler<DomainEventNotification<BadgeProgressReviewed>>
{
    private readonly INotificationService _notifications;

    public BadgeProgressReviewedEventHandler(INotificationService notifications)
    {
        _notifications = notifications;
    }

    public Task Handle(DomainEventNotification<BadgeProgressReviewed> notification, CancellationToken cancellationToken)
    {
        var raised = notification.Event;

        var (title, body) = raised switch
        {
            { IsApproved: true } => ("Вмілість зараховано",
                "Вашу вмілість зараховано."),
            { ConfirmationWithdrawn: true } => ("Підтвердження вмілості скасовано",
                "Раніше зараховану вмілість вилучено."),
            _ => ("Вмілість потребує доопрацювання",
                "Вашу вмілість не зараховано. Перегляньте зауваження та подайте її повторно.")
        };

        return _notifications.NotifyAsync(
            new NotificationRequest
            {
                RecipientUserKey = raised.MemberUserKey,
                Type = AppNotificationType.MemberSkillReviewed,
                Severity = raised.IsApproved ? AppNotificationSeverity.Success : AppNotificationSeverity.Warn,
                Title = title,
                Body = body,
                EntityType = "BadgeProgress",
                EntityKey = raised.BadgeProgressKey,
                Route = $"/member/{raised.MemberKey}",
                ActorUserKey = raised.ActorUserKey,
                DeduplicationKey = $"skill-review-result:{raised.MemberKey}:{raised.BadgeId}"
            },
            cancellationToken);
    }
}
