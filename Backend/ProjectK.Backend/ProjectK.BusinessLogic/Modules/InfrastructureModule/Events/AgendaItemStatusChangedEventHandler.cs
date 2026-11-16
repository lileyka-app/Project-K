using MediatR;
using ProjectK.BusinessLogic.Modules.InfrastructureModule.Services;
using ProjectK.BusinessLogic.Services.Events;
using ProjectK.Common.Contracts.InfrastructureModule;
using ProjectK.Common.Dtos.InfrastructureModule;
using ProjectK.Common.Enums.AgendaModule;
using ProjectK.Common.Enums.InfrastructureModule;
using ProjectK.Common.Events.AgendaModule;

namespace ProjectK.BusinessLogic.Modules.InfrastructureModule.Events;

public sealed class AgendaItemStatusChangedEventHandler
    : INotificationHandler<DomainEventNotification<AgendaItemStatusChanged>>
{
    private readonly INotificationService _notifications;

    public AgendaItemStatusChangedEventHandler(INotificationService notifications)
    {
        _notifications = notifications;
    }

    public Task Handle(DomainEventNotification<AgendaItemStatusChanged> notification, CancellationToken cancellationToken)
    {
        var raised = notification.Event;

        return _notifications.NotifyAsync(
            new NotificationRequest
            {
                RecipientUserKey = raised.CreatorUserKey,
                Type = AppNotificationType.AgendaItemStatusChanged,
                Severity = raised.Status == AgendaItemStatus.Done
                    ? AppNotificationSeverity.Success
                    : AppNotificationSeverity.Info,
                Title = "Статус задачі змінено",
                Body = raised.Title,
                EntityType = "AgendaItem",
                EntityKey = raised.AgendaItemKey,
                Route = AgendaNotificationNames.RouteFor(raised.Kind, raised.KurinKey),
                ActorUserKey = raised.ActorUserKey,
                DeduplicationKey = $"agenda-status:{raised.AgendaItemKey}"
            },
            cancellationToken);
    }
}
