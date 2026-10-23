using Microsoft.EntityFrameworkCore;
using ProjectK.Common.Entities.AuthModule;
using ProjectK.Common.Interfaces.Modules.AuthModule;
using ProjectK.Common.Models.Enums;
using ProjectK.Infrastructure.DbContexts;

namespace ProjectK.Infrastructure.Repositories.AuthModule;

/// <inheritdoc />
public sealed class CalendarFeedStore : ICalendarFeedStore
{
    private static readonly TimeSpan FetchStampInterval = TimeSpan.FromHours(1);

    private readonly AppDbContext _context;
    private readonly DbContextOptions<AppDbContext> _options;
    private readonly TimeProvider _timeProvider;

    public CalendarFeedStore(AppDbContext context, DbContextOptions<AppDbContext> options, TimeProvider timeProvider)
    {
        _context = context;
        _options = options;
        _timeProvider = timeProvider;
    }

    public Task<CalendarFeed?> FindByTokenHashAsync(string tokenHash, CancellationToken cancellationToken = default) =>
        _context.CalendarFeeds
            .AsNoTracking()
            .FirstOrDefaultAsync(feed => feed.TokenHash == tokenHash, cancellationToken);

    public Task<CalendarFeed?> GetLatestForUserAsync(Guid userId, CancellationToken cancellationToken = default) =>
        _context.CalendarFeeds
            .AsNoTracking()
            .Where(feed => feed.UserId == userId)
            .OrderBy(feed => feed.RevokedAtUtc == null ? 0 : 1)
            .ThenByDescending(feed => feed.RevokedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

    /// <summary>
    /// Added through a context of its own, for the reason <see cref="RefreshTokenStore.IssueAsync"/>
    /// gives: the scoped context is shared with the unit of work.
    /// </summary>
    public async Task IssueAsync(Guid userId, string tokenHash, bool includeSchedules, bool includeTasks, CancellationToken cancellationToken = default)
    {
        await RevokeActiveAsync(userId, CalendarFeedRevokeReason.Reissued, cancellationToken);

        await using var context = new AppDbContext(_options);
        context.CalendarFeeds.Add(new CalendarFeed
        {
            UserId = userId,
            TokenHash = tokenHash,
            IncludeSchedules = includeSchedules,
            IncludeTasks = includeTasks
        });
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task<bool> UpdateOptionsAsync(Guid userId, bool includeSchedules, bool includeTasks, CancellationToken cancellationToken = default)
    {
        var now = _timeProvider.GetUtcNow().UtcDateTime;

        var updated = await _context.CalendarFeeds
            .Where(feed => feed.UserId == userId && feed.RevokedAtUtc == null)
            .ExecuteUpdateAsync(
                feed => feed
                    .SetProperty(row => row.IncludeSchedules, includeSchedules)
                    .SetProperty(row => row.IncludeTasks, includeTasks)
                    .SetProperty(row => row.UpdatedDate, now),
                cancellationToken);

        return updated > 0;
    }

    public async Task<bool> RevokeActiveAsync(Guid userId, CalendarFeedRevokeReason reason, CancellationToken cancellationToken = default)
    {
        var now = _timeProvider.GetUtcNow().UtcDateTime;

        var revoked = await _context.CalendarFeeds
            .Where(feed => feed.UserId == userId && feed.RevokedAtUtc == null)
            .ExecuteUpdateAsync(
                feed => feed
                    .SetProperty(row => row.RevokedAtUtc, now)
                    .SetProperty(row => row.RevokedReason, reason),
                cancellationToken);

        return revoked > 0;
    }

    public async Task MarkFetchedAsync(Guid calendarFeedKey, CancellationToken cancellationToken = default)
    {
        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var staleBefore = now - FetchStampInterval;

        await _context.CalendarFeeds
            .Where(feed => feed.CalendarFeedKey == calendarFeedKey
                && (feed.LastFetchedAtUtc == null || feed.LastFetchedAtUtc < staleBefore))
            .ExecuteUpdateAsync(feed => feed.SetProperty(row => row.LastFetchedAtUtc, now), cancellationToken);
    }
}
