using MediatR;
using ProjectK.BusinessLogic.Modules.NotificationsModule.Services;
using ProjectK.BusinessLogic.Services.Events;
using ProjectK.Common.Contracts.NotificationsModule;
using ProjectK.Common.Dtos.NotificationsModule;
using ProjectK.Common.Enums.NotificationsModule;
using ProjectK.Common.Events.AgendaModule;

namespace ProjectK.BusinessLogic.Modules.NotificationsModule.Events;

public sealed class AgendaItemAssignedEventHandler
    : INotificationHandler<DomainEventNotification<AgendaItemAssigned>>
{
    private readonly INotificationService _notifications;

    public AgendaItemAssignedEventHandler(INotificationService notifications)
    {
        _notifications = notifications;
    }

    public async Task Handle(DomainEventNotification<AgendaItemAssigned> notification, CancellationToken cancellationToken)
    {
        var raised = notification.Event;
        if (raised.RecipientUserKeys.Count == 0)
        {
            return;
        }

        await _notifications.NotifyManyAsync(
            raised.RecipientUserKeys.Select(userKey => new NotificationRequest
            {
                RecipientUserKey = userKey,
                Type = AppNotificationType.AgendaItemAssigned,
                Severity = AppNotificationSeverity.Info,
                Title = $"{AgendaNotificationNames.KindWord(raised.Kind)} призначено вам",
                Body = raised.Title,
                EntityType = "AgendaItem",
                EntityKey = raised.AgendaItemKey,
                Route = AgendaNotificationNames.RouteFor(raised.Kind, raised.KurinKey),
                ActorUserKey = raised.ActorUserKey,
                DeduplicationKey = $"agenda-assigned:{raised.AgendaItemKey}:{userKey}"
            }),
            cancellationToken);
    }
}
