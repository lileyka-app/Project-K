using MediatR;
using ProjectK.BusinessLogic.Services.Events;
using ProjectK.Common.Contracts.NotificationsModule;
using ProjectK.Common.Dtos.NotificationsModule;
using ProjectK.Common.Enums.NotificationsModule;
using ProjectK.Common.Events.ProbesAndBadgesModule;

namespace ProjectK.BusinessLogic.Modules.NotificationsModule.Events;

public sealed class BadgeProgressSubmittedEventHandler
    : INotificationHandler<DomainEventNotification<BadgeProgressSubmitted>>
{
    private readonly INotificationService _notifications;
    private readonly IReviewNotificationRecipientReader _recipients;

    public BadgeProgressSubmittedEventHandler(
        INotificationService notifications,
        IReviewNotificationRecipientReader recipients)
    {
        _notifications = notifications;
        _recipients = recipients;
    }

    public async Task Handle(DomainEventNotification<BadgeProgressSubmitted> notification, CancellationToken cancellationToken)
    {
        var raised = notification.Event;
        var recipientUserKeys = await _recipients.ResolveAsync(
            ReviewSubject.Badge,
            raised.KurinKey,
            raised.GroupKey,
            raised.ActorUserKey,
            cancellationToken);

        if (recipientUserKeys.Count == 0)
        {
            return;
        }

        await _notifications.NotifyManyAsync(
            recipientUserKeys.Select(userKey => new NotificationRequest
            {
                RecipientUserKey = userKey,
                Type = AppNotificationType.MemberSkillSubmittedForReview,
                Severity = AppNotificationSeverity.Info,
                Title = "Вмілість подано на перевірку",
                Body = string.IsNullOrWhiteSpace(raised.MemberName)
                    ? "Надійшла вмілість на перевірку."
                    : $"Надійшла вмілість від {raised.MemberName} на перевірку.",
                EntityType = "BadgeProgress",
                EntityKey = raised.MemberKey,
                Route = $"/kurin/{raised.KurinKey}/review/skills",
                ActorUserKey = raised.ActorUserKey,
                DeduplicationKey = $"skill-review:{raised.MemberKey}:{raised.BadgeId}"
            }),
            cancellationToken);
    }
}
