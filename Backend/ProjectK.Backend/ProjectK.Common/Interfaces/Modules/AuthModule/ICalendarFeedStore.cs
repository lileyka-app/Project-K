using ProjectK.Common.Entities.AuthModule;
using ProjectK.Common.Models.Enums;

namespace ProjectK.Common.Interfaces.Modules.AuthModule;

/// <summary>
/// The calendar links of accounts (see <see cref="CalendarFeed"/>). Like the session store, every
/// write commits on its own: revoking a link is part of a security change that is already stored.
/// </summary>
public interface ICalendarFeedStore
{
    /// <summary>The link behind a token hash, live or revoked; null when there is none.</summary>
    Task<CalendarFeed?> FindByTokenHashAsync(string tokenHash, CancellationToken cancellationToken = default);

    /// <summary>The account's live link, else its most recently revoked one.</summary>
    Task<CalendarFeed?> GetLatestForUserAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>Stores a new live link and revokes the one it replaces.</summary>
    Task IssueAsync(Guid userId, string tokenHash, bool includeSchedules, bool includeTasks, CancellationToken cancellationToken = default);

    /// <summary>Changes what the live link carries; false when there is no live link.</summary>
    Task<bool> UpdateOptionsAsync(Guid userId, bool includeSchedules, bool includeTasks, CancellationToken cancellationToken = default);

    /// <summary>Revokes the live link; answers whether there was one.</summary>
    Task<bool> RevokeActiveAsync(Guid userId, CalendarFeedRevokeReason reason, CancellationToken cancellationToken = default);

    /// <summary>Stamps a fetch, unless one was stamped within the last hour.</summary>
    Task MarkFetchedAsync(Guid calendarFeedKey, CancellationToken cancellationToken = default);
}
