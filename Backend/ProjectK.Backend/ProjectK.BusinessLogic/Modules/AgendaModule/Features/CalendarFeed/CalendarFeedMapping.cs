using ProjectK.Common.Dtos.AgendaModule;
using ProjectK.Common.Enums.AgendaModule;
using CalendarFeedEntity = ProjectK.Common.Entities.AgendaModule.CalendarFeed;

namespace ProjectK.BusinessLogic.Modules.AgendaModule.Features.CalendarFeed;

/// <summary>Maps the account's calendar link to what the client sees of it.</summary>
public static class CalendarFeedMapping
{
    /// <summary>Where the link stands; no link at all is <see cref="CalendarFeedState.None"/>.</summary>
    public static CalendarFeedStatusDto ToDto(CalendarFeedEntity? feed) => feed is null
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
