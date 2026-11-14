using ProjectK.Common.Models.Dtos.InfrastructureModule;

namespace ProjectK.Common.Contracts.InfrastructureModule;

public interface INotificationService
{
    Task<AppNotificationDto?> NotifyAsync(NotificationRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AppNotificationDto>> NotifyManyAsync(IEnumerable<NotificationRequest> requests, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AppNotificationDto>> GetInboxAsync(NotificationQuery query, CancellationToken cancellationToken = default);
    Task<int> GetUnreadCountAsync(CancellationToken cancellationToken = default);
    Task<AppNotificationDto?> MarkAsReadAsync(Guid notificationKey, CancellationToken cancellationToken = default);
    Task<int> MarkAllAsReadAsync(CancellationToken cancellationToken = default);
}
