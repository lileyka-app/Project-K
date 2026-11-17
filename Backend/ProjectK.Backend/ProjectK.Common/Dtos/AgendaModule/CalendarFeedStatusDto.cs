using ProjectK.Common.Enums.AgendaModule;

namespace ProjectK.Common.Dtos.AgendaModule;

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
}
