using Microsoft.EntityFrameworkCore;
using ProjectK.Common.Contracts.NotificationsModule;
using ProjectK.Common.Entities.NotificationsModule;
using ProjectK.Infrastructure.DbContexts;

namespace ProjectK.Infrastructure.Repositories.NotificationsModule;

public class AppNotificationRepository : BaseEntityRepository<AppNotification>, IAppNotificationRepository
{

    public AppNotificationRepository(AppDbContext context) : base(context)
    {
    }

    public override async Task<AppNotification?> GetByKeyAsync(Guid entityKey, CancellationToken cancellationToken = default)
    {
        return await Context.AppNotifications
            .AsTracking()
            .FirstOrDefaultAsync(x => x.NotificationKey == entityKey, cancellationToken);
    }

    public override async Task<bool> ExistsAsync(Guid entityKey, CancellationToken cancellationToken = default)
    {
        return await Context.AppNotifications
            .AnyAsync(x => x.NotificationKey == entityKey, cancellationToken);
    }

    public override void Update(AppNotification entity, CancellationToken cancellationToken = default) => MarkModified(entity);

    public async Task<IReadOnlyList<AppNotification>> GetInboxAsync(
        Guid recipientUserKey,
        bool unreadOnly,
        int take,
        DateTime nowUtc,
        CancellationToken cancellationToken = default)
    {
        var query = ActiveForRecipient(recipientUserKey, nowUtc)
            .AsNoTracking();

        if (unreadOnly)
        {
            query = query.Where(x => x.ReadAtUtc == null);
        }

        return await query
            .OrderByDescending(x => x.CreatedAtUtc)
            .Take(take)
            .ToListAsync(cancellationToken);
    }

    public async Task<int> GetUnreadCountAsync(
        Guid recipientUserKey,
        DateTime nowUtc,
        CancellationToken cancellationToken = default)
    {
        return await ActiveForRecipient(recipientUserKey, nowUtc)
            .Where(x => x.ReadAtUtc == null)
            .CountAsync(cancellationToken);
    }

    public async Task<AppNotification?> GetByRecipientAndKeyAsync(
        Guid recipientUserKey,
        Guid notificationKey,
        CancellationToken cancellationToken = default)
    {
        return await Context.AppNotifications
            .AsTracking()
            .FirstOrDefaultAsync(
                x => x.RecipientUserKey == recipientUserKey
                     && x.NotificationKey == notificationKey,
                cancellationToken);
    }

    public async Task<AppNotification?> GetUnreadByDeduplicationKeyAsync(
        Guid recipientUserKey,
        string deduplicationKey,
        DateTime nowUtc,
        CancellationToken cancellationToken = default)
    {
        return await ActiveForRecipient(recipientUserKey, nowUtc)
            .AsTracking()
            .FirstOrDefaultAsync(
                x => x.ReadAtUtc == null
                     && x.DeduplicationKey == deduplicationKey,
                cancellationToken);
    }

    public async Task<int> MarkAllAsReadAsync(
        Guid recipientUserKey,
        DateTime readAtUtc,
        CancellationToken cancellationToken = default)
    {
        return await Context.AppNotifications
            .Where(x => x.RecipientUserKey == recipientUserKey && x.ReadAtUtc == null)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(x => x.ReadAtUtc, readAtUtc)
                .SetProperty(x => x.UpdatedDate, readAtUtc),
                cancellationToken);
    }

    private IQueryable<AppNotification> ActiveForRecipient(Guid recipientUserKey, DateTime nowUtc)
    {
        return Context.AppNotifications
            .Where(x => x.RecipientUserKey == recipientUserKey
                        && (x.ExpiresAtUtc == null || x.ExpiresAtUtc > nowUtc));
    }
}
