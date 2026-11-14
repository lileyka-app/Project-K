namespace ProjectK.Common.Models.Dtos.ScoreModule;

public sealed record PrivateScoreDto
{
    public required Guid KurinKey { get; init; }
    public required ScorePeriodDto Period { get; init; }
    public required ScorePeriodsDto Periods { get; init; }
    public required IReadOnlyList<PrivateScoreCriterionDto> Criteria { get; init; }
    public required IReadOnlyList<PrivateScorePersonDto> People { get; init; }
    public required IReadOnlyList<PrivateScoreEntryDto> Entries { get; init; }
}
