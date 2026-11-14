using System.Threading.Channels;
using ProjectK.Common.Contracts.InfrastructureModule;
using ProjectK.Common.Models.Dtos.InfrastructureModule;

namespace ProjectK.Infrastructure.Services.WebPush;

/// <summary>
/// The hand-over between the request that saved a notification and <see cref="BackgroundServices.WebPushBackgroundService"/>.
/// In memory and bounded: a push is a courtesy on top of the inbox, so a restart or a burst past the
/// bound loses the oldest pushes, never a notification.
/// </summary>
public sealed class ChannelPushQueue : IPushQueue
{
    private readonly Channel<AppNotificationDto> _channel = Channel.CreateBounded<AppNotificationDto>(
        new BoundedChannelOptions(1000) { FullMode = BoundedChannelFullMode.DropOldest, SingleReader = true });

    public ChannelReader<AppNotificationDto> Reader => _channel.Reader;

    public void Enqueue(AppNotificationDto notification) => _channel.Writer.TryWrite(notification);
}

/// <summary>Push is not configured here (no VAPID keys): notifications stay in the app.</summary>
public sealed class DisabledPushQueue : IPushQueue
{
    public void Enqueue(AppNotificationDto notification)
    {
        // Nothing to send with.
    }
}
