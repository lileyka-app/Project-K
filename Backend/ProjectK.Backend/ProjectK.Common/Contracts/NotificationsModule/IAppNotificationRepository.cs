using ProjectK.Common.Contracts.Shared;
using ProjectK.Common.Entities.NotificationsModule;

namespace ProjectK.Common.Contracts.NotificationsModule;

public interface IAppNotificationRepository : IBaseEntityRepository<AppNotification>
{
    Task<IReadOnlyList<AppNotification>> GetInboxAsync(
        Guid recipientUserKey,
        bool unreadOnly,
        int take,
        DateTime nowUtc,
        CancellationToken cancellationToken = default);

    Task<int> GetUnreadCountAsync(
        Guid recipientUserKey,
        DateTime nowUtc,
        CancellationToken cancellationToken = default);

    Task<AppNotification?> GetByRecipientAndKeyAsync(
        Guid recipientUserKey,
        Guid notificationKey,
        CancellationToken cancellationToken = default);

    Task<AppNotification?> GetUnreadByDeduplicationKeyAsync(
        Guid recipientUserKey,
        string deduplicationKey,
        DateTime nowUtc,
        CancellationToken cancellationToken = default);

    Task<int> MarkAllAsReadAsync(
        Guid recipientUserKey,
        DateTime readAtUtc,
        CancellationToken cancellationToken = default);
}
