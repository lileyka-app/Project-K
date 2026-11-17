namespace ProjectK.Common.Dtos.ScoreModule;

public sealed record PrivateScoreEntryDto
{
    public required Guid PrivateScoreEntryKey { get; init; }
    public required Guid MembershipKey { get; init; }
    public required Guid MemberKey { get; init; }
    public required string MemberName { get; init; }
    public required Guid? PrivateScoreCriterionKey { get; init; }
    public required string? CriterionName { get; init; }
    public required int Points { get; init; }
    public required string? Note { get; init; }
    public required DateOnly OccurredOn { get; init; }
    public required string? CreatedByName { get; init; }
    public required DateTime CreatedAtUtc { get; init; }
}
