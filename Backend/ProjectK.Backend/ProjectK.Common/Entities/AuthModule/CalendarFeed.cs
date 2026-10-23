using ProjectK.Common.Models.Enums;

namespace ProjectK.Common.Entities.AuthModule;

/// <summary>
/// The secret link an account subscribes to from Google, Apple or Outlook: an iCalendar file of the
/// person's events across every kurin they stand in.
/// <para>
/// The calendar app fetches it from its own servers, with no cookie and no header, so the token in
/// the path is the whole of the access. Only its SHA-256 is kept: the link is shown once, when it is
/// issued, and a lost link is reissued rather than read back.
/// </para>
/// <para>
/// A revoked row is kept for a while on purpose. A subscription that silently stops updating looks
/// like a calendar with no events, so the old link keeps answering with a notice that it is off.
/// </para>
/// </summary>
public class CalendarFeed : Entity
{
    public Guid CalendarFeedKey { get; set; } = Guid.NewGuid();

    public Guid UserId { get; set; }

    /// <summary>Lowercase hex SHA-256 of the token in the link.</summary>
    public string TokenHash { get; set; } = null!;

    /// <summary>Every гурток's сходини, not only one's own.</summary>
    public bool IncludeSchedules { get; set; }

    /// <summary>Dated tasks, as all-day events: calendar apps do not show VTODO from a subscription.</summary>
    public bool IncludeTasks { get; set; }

    /// <summary>Last time a calendar app fetched it; written at most hourly, so polling is not a write each time.</summary>
    public DateTime? LastFetchedAtUtc { get; set; }

    public DateTime? RevokedAtUtc { get; set; }

    public CalendarFeedRevokeReason? RevokedReason { get; set; }

    public AppUser User { get; set; } = null!;
}
