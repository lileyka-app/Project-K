using ProjectK.Common.Entities.AuthModule;
using ProjectK.Common.Enums.AuthModule;

namespace ProjectK.Common.Contracts.AuthModule;

/// <summary>
/// The auth module's write door for ending an account's sessions and its calendar link, for the
/// account changes other modules make: a new password, a confirmed email change, a suspension.
/// <para>
/// A write contract on purpose. An event does not fit: the change is made because of a suspected
/// compromise, and the caller answers only once every older session is gone — an event handled after
/// the reply would leave an intruder signed in for that moment. Call it once the change is stored.
/// </para>
/// </summary>
public interface IAccountSessionWriter
{
    /// <summary>Ends every session of the account and switches off its calendar link, as a security change.</summary>
    Task RevokeRefreshTokenAsync(AppUser user, CancellationToken cancellationToken = default);

    /// <summary>Ends every session of the account and switches off its calendar link, for <paramref name="reason"/>.</summary>
    Task RevokeRefreshTokenAsync(AppUser user, CalendarFeedRevokeReason reason, CancellationToken cancellationToken = default);

    /// <summary>The calendar link alone, for a change that does not end sessions.</summary>
    Task RevokeCalendarFeedAsync(Guid userId, CalendarFeedRevokeReason reason, CancellationToken cancellationToken = default);
}
