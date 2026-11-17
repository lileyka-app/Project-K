using ProjectK.Common.Contracts.NotificationsModule;
using ProjectK.Common.Dtos.NotificationsModule;

namespace ProjectK.Infrastructure.Services.WebPush;

/// <summary>Push is not configured here (no VAPID keys): notifications stay in the app.</summary>
public sealed class DisabledPushQueue : IPushQueue
{
    public void Enqueue(AppNotificationDto notification)
    {
        // Nothing to send with.
    }
}
