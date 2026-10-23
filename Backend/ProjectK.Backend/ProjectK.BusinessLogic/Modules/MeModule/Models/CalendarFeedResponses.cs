using ProjectK.Common.Entities.AuthModule;
using ProjectK.Common.Models.Enums;

namespace ProjectK.BusinessLogic.Modules.MeModule.Models;

public enum CalendarFeedState
{
    None = 0,
    Active = 1,
    Revoked = 2
}

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

/// <summary>A freshly issued link: the only time its token leaves the server.</summary>
public sealed class IssuedCalendarFeedDto
{
    public string Token { get; set; } = string.Empty;
    public CalendarFeedStatusDto Status { get; set; } = new();
}

/// <summary>What the calendar link answers: a file, or nothing for a token that was never issued.</summary>
public sealed record CalendarFeedFile(string Content);
