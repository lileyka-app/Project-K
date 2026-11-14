namespace ProjectK.Common.Dtos.ScoreModule;

public sealed record ScoreEntryDto
{
    public required Guid ScoreEntryKey { get; init; }
    public required Guid? MembershipKey { get; init; }
    public required Guid? MemberKey { get; init; }
    public required string? MemberName { get; init; }
    public required Guid? GroupKey { get; init; }
    public required string GroupName { get; init; }
    /// <summary>Given to the гурток as a whole, not to a person in it.</summary>
    public required bool IsForGroup { get; init; }
    public required Guid? ScoreItemKey { get; init; }
    public required string? ItemName { get; init; }
    public required int Points { get; init; }
    public required string? Reason { get; init; }
    public required Guid? AgendaItemKey { get; init; }
    public required DateTime? OccurrenceStartUtc { get; init; }
    public required DateOnly OccurredOn { get; init; }
    public required string? CreatedByName { get; init; }
    public required DateTime CreatedAtUtc { get; init; }
}
