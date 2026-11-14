namespace ProjectK.Common.Models.Dtos.ScoreModule;

public sealed record ScorePeriodsDto
{
    public required IReadOnlyList<ScorePeriodDto> Years { get; init; }
    public required IReadOnlyList<ScorePeriodDto> Stages { get; init; }
}
