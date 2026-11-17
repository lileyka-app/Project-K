using MediatR;
using ProjectK.BusinessLogic.Services.Events;
using ProjectK.Common.Contracts.NotificationsModule;
using ProjectK.Common.Dtos.NotificationsModule;
using ProjectK.Common.Enums.NotificationsModule;
using ProjectK.Common.Events.MemberModule;

namespace ProjectK.BusinessLogic.Modules.NotificationsModule.Events;

public sealed class MemberAwardSubmittedEventHandler
    : INotificationHandler<DomainEventNotification<MemberAwardSubmitted>>
{
    private readonly INotificationService _notifications;
    private readonly IReviewNotificationRecipientReader _recipients;

    public MemberAwardSubmittedEventHandler(
        INotificationService notifications,
        IReviewNotificationRecipientReader recipients)
    {
        _notifications = notifications;
        _recipients = recipients;
    }

    public async Task Handle(DomainEventNotification<MemberAwardSubmitted> notification, CancellationToken cancellationToken)
    {
        var raised = notification.Event;
        var recipientUserKeys = await _recipients.ResolveAsync(
            ReviewSubject.Award,
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
                Type = AppNotificationType.MemberAwardSubmitted,
                Severity = AppNotificationSeverity.Info,
                Title = "Відзначення подано на розгляд",
                Body = string.IsNullOrWhiteSpace(raised.MemberName)
                    ? "Надійшло відзначення на розгляд."
                    : $"Надійшло відзначення від {raised.MemberName} на розгляд.",
                EntityType = "MemberAward",
                EntityKey = raised.MemberAwardKey,
                Route = $"/member/{raised.MemberKey}",
                ActorUserKey = raised.ActorUserKey,
                DeduplicationKey = $"award-submitted:{raised.MemberAwardKey}"
            }),
            cancellationToken);
    }
}
