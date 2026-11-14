using ProjectK.Common.Enums.KurinModule;

namespace ProjectK.Common.Dtos.ScoreModule;

public sealed record SheetPersonDto
{
    public required Guid MembershipKey { get; init; }
    public required Guid MemberKey { get; init; }
    public required string FullName { get; init; }
    public required Guid? GroupKey { get; init; }
    public required string GroupName { get; init; }
    /// <summary>The person's answer to the invitation, if they gave one.</summary>
    public required AgendaRsvpStatus? Rsvp { get; init; }
    /// <summary>Whether the event was aimed at them — at the kurin, their гурток or them by name.</summary>
    public required bool IsAssigned { get; init; }
    public required AttendanceMarkDto? Attendance { get; init; }
    /// <summary>Whether the caller may mark or score this person.</summary>
    public required bool CanScore { get; init; }
    public required IReadOnlyList<ScoreEntryDto> Entries { get; init; }
}
