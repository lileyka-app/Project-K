using ProjectK.Common.Dtos.NotificationsModule;
using ProjectK.Common.Entities.NotificationsModule;

namespace ProjectK.BusinessLogic.Modules.NotificationsModule.Features.Notifications;

internal static class NotificationMapping
{
    public static AppNotificationDto ToDto(this AppNotification notification) => new()
    {
        NotificationKey = notification.NotificationKey,
        RecipientUserKey = notification.RecipientUserKey,
        Type = notification.Type,
        Severity = notification.Severity,
        Title = notification.Title,
        Body = notification.Body,
        EntityType = notification.EntityType,
        EntityKey = notification.EntityKey,
        Route = notification.Route,
        PayloadJson = notification.PayloadJson,
        CreatedAtUtc = notification.CreatedAtUtc,
        ReadAtUtc = notification.ReadAtUtc,
        ActorUserKey = notification.ActorUserKey,
        DeduplicationKey = notification.DeduplicationKey,
        ExpiresAtUtc = notification.ExpiresAtUtc
    };
}
