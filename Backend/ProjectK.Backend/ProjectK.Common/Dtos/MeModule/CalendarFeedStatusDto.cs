using ProjectK.Common.Entities.AuthModule;
using ProjectK.Common.Models.Enums;

namespace ProjectK.Common.Dtos.MeModule;

/// <summary>Where the account's calendar link stands. The link itself is never in it: only its hash is kept.</summary>
public sealed class CalendarFeedStatusDto
{
    public CalendarFeedState State { get; set; }
    public bool IncludeSchedules { get; set; }
    public bool IncludeTasks { get; set; }
    public DateTime? CreatedUtc { get; set; }
    public DateTime? LastFetchedUtc { get; set; }
    public DateTime? RevokedUtc { get; set; }
    public CalendarFeedRevokeReason? RevokedReason { get; set; }

    public static CalendarFeedStatusDto Of(CalendarFeed? feed) => feed is null
        ? new CalendarFeedStatusDto { State = CalendarFeedState.None }
        : new CalendarFeedStatusDto
        {
            State = feed.RevokedAtUtc is null ? CalendarFeedState.Active : CalendarFeedState.Revoked,
            IncludeSchedules = feed.IncludeSchedules,
            IncludeTasks = feed.IncludeTasks,
            CreatedUtc = Utc(feed.CreatedDate),
            LastFetchedUtc = feed.LastFetchedAtUtc is { } fetched ? Utc(fetched) : null,
            RevokedUtc = feed.RevokedAtUtc is { } revoked ? Utc(revoked) : null,
            RevokedReason = feed.RevokedReason
        };

    private static DateTime Utc(DateTime value) => DateTime.SpecifyKind(value, DateTimeKind.Utc);
}
