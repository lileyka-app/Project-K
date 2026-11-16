using MediatR;
using ProjectK.BusinessLogic.Modules.InfrastructureModule.Services;
using ProjectK.BusinessLogic.Services.Events;
using ProjectK.Common.Contracts.InfrastructureModule;
using ProjectK.Common.Dtos.InfrastructureModule;
using ProjectK.Common.Enums.InfrastructureModule;
using ProjectK.Common.Events.AgendaModule;

namespace ProjectK.BusinessLogic.Modules.InfrastructureModule.Events;

public sealed class AgendaItemChangedEventHandler
    : INotificationHandler<DomainEventNotification<AgendaItemChanged>>
{
    private readonly INotificationService _notifications;

    public AgendaItemChangedEventHandler(INotificationService notifications)
    {
        _notifications = notifications;
    }

    public async Task Handle(DomainEventNotification<AgendaItemChanged> notification, CancellationToken cancellationToken)
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
                Type = AppNotificationType.AgendaItemUpdated,
                Severity = AppNotificationSeverity.Info,
                Title = "Оновлено призначення",
                Body = raised.Title,
                EntityType = "AgendaItem",
                EntityKey = raised.AgendaItemKey,
                Route = AgendaNotificationNames.RouteFor(raised.Kind, raised.KurinKey),
                ActorUserKey = raised.ActorUserKey,
                DeduplicationKey = $"agenda-updated:{raised.AgendaItemKey}:{userKey}"
            }),
            cancellationToken);
    }
}
