namespace ProjectK.Common.Dtos.ScoreModule;

public sealed record ScorePeriodDto
{
    /// <summary><c>Year</c> or <c>Stage</c>.</summary>
    public required string Kind { get; init; }
    public required int? Year { get; init; }
    public required Guid? StageKey { get; init; }
    public required string Label { get; init; }
    public required DateOnly From { get; init; }
    public required DateOnly To { get; init; }
}
