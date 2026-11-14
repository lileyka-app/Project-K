namespace ProjectK.Common.Models.Dtos.ScoreModule;

/// <summary>Who was at one occurrence of an event, and what was given there.</summary>
public sealed record AttendanceSheetDto
{
    public required Guid KurinKey { get; init; }
    public required Guid AgendaItemKey { get; init; }
    public required DateTime OccurrenceStartUtc { get; init; }
    public required DateTime? OccurrenceEndUtc { get; init; }
    public required bool IsAllDay { get; init; }
    public required bool IsRecurring { get; init; }
    public required string Title { get; init; }
    public required Guid? CategoryKey { get; init; }
    public required string? CategoryName { get; init; }
    public required string? CategoryColorHex { get; init; }
    public required string? CategoryIcon { get; init; }
    /// <summary>What a mark here is worth now.</summary>
    public required int AttendancePoints { get; init; }
    /// <summary>Whether the event has a rate of its own, over its group's.</summary>
    public required bool HasOwnRate { get; init; }
    public required IReadOnlyList<SheetPersonDto> People { get; init; }
    public required IReadOnlyList<SheetGroupDto> Groups { get; init; }
    public required IReadOnlyList<ScoreItemDto> Items { get; init; }
    public required bool CanManage { get; init; }
    /// <summary>Set when someone said the occurrence did not take place; nobody is marked then.</summary>
    public required OccurrenceNotHeldDto? NotHeld { get; init; }
}
