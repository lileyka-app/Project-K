using ProjectK.Common.Contracts.Shared;
using ProjectK.Common.Entities.NotificationsModule;

namespace ProjectK.Common.Contracts.NotificationsModule;

public interface IWebPushSubscriptionRepository : IBaseEntityRepository<WebPushSubscription>
{
    /// <summary>The device's row, tracked, or <c>null</c>. Endpoints are unique across accounts.</summary>
    Task<WebPushSubscription?> GetByEndpointAsync(string endpoint, CancellationToken cancellationToken = default);

    /// <summary>Every device the account has registered, tracked so a dead one can be removed.</summary>
    Task<IReadOnlyList<WebPushSubscription>> GetByUserAsync(Guid userKey, CancellationToken cancellationToken = default);
}
