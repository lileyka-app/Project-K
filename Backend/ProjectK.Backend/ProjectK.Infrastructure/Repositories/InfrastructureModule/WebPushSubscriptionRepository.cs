using Microsoft.EntityFrameworkCore;
using ProjectK.Common.Contracts.InfrastructureModule;
using ProjectK.Common.Entities.InfrastructureModule;
using ProjectK.Infrastructure.DbContexts;

namespace ProjectK.Infrastructure.Repositories.InfrastructureModule;

public class WebPushSubscriptionRepository : BaseEntityRepository<WebPushSubscription>, IWebPushSubscriptionRepository
{
    public WebPushSubscriptionRepository(AppDbContext context) : base(context)
    {
    }

    public async Task<WebPushSubscription?> GetByEndpointAsync(string endpoint, CancellationToken cancellationToken = default)
    {
        return await Context.WebPushSubscriptions
            .AsTracking()
            .FirstOrDefaultAsync(x => x.Endpoint == endpoint, cancellationToken);
    }

    public async Task<IReadOnlyList<WebPushSubscription>> GetByUserAsync(Guid userKey, CancellationToken cancellationToken = default)
    {
        return await Context.WebPushSubscriptions
            .AsTracking()
            .Where(x => x.UserKey == userKey)
            .OrderBy(x => x.CreatedAtUtc)
            .ToListAsync(cancellationToken);
    }
}
