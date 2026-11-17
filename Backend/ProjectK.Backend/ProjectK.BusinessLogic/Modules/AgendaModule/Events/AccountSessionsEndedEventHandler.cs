using MediatR;
using ProjectK.BusinessLogic.Services.Events;
using ProjectK.Common.Contracts.AgendaModule;
using ProjectK.Common.Contracts.NotificationsModule;
using ProjectK.Common.Dtos.NotificationsModule;
using ProjectK.Common.Enums.AgendaModule;
using ProjectK.Common.Enums.IdentityModule;
using ProjectK.Common.Enums.NotificationsModule;
using ProjectK.Common.Events.IdentityModule;
using ProjectK.Common.Rules.AgendaModule;

namespace ProjectK.BusinessLogic.Modules.AgendaModule.Events;

/// <summary>
/// Switches off the calendar link of an account whose sessions were just ended. The link is access
/// too — an intruder could have issued one — so it goes with the sessions, and the person is told,
/// since a subscription that stops updating says nothing by itself. The store commits on its own,
/// and so does the notification.
/// </summary>
public sealed class AccountSessionsEndedEventHandler : INotificationHandler<DomainEventNotification<AccountSessionsEnded>>
{
    private readonly ICalendarFeedStore _calendarFeeds;
    private readonly INotificationService _notifications;

    public AccountSessionsEndedEventHandler(ICalendarFeedStore calendarFeeds, INotificationService notifications)
    {
        _calendarFeeds = calendarFeeds;
        _notifications = notifications;
    }

    public async Task Handle(DomainEventNotification<AccountSessionsEnded> notification, CancellationToken cancellationToken)
    {
        var userKey = notification.Event.UserKey;
        var reason = notification.Event.Reason == SessionEndReason.Suspended
            ? CalendarFeedRevokeReason.Suspended
            : CalendarFeedRevokeReason.SecurityChange;

        if (!await _calendarFeeds.RevokeActiveAsync(userKey, reason, cancellationToken))
        {
            return;
        }

        await _notifications.NotifyAsync(new NotificationRequest
        {
            RecipientUserKey = userKey,
            Type = AppNotificationType.CalendarFeedRevoked,
            Severity = AppNotificationSeverity.Warn,
            Title = "Посилання на календар вимкнено",
            Body = "Після зміни в безпеці акаунта старе посилання більше не оновлює твій календар. Створи нове в налаштуваннях і додай його в календар знову.",
            Route = CalendarFeedNames.SettingsRoute,
            DeduplicationKey = $"calendar-feed-revoked:{userKey}"
        }, cancellationToken);
    }
}
