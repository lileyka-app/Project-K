using ProjectK.Common.Models.Enums;

namespace ProjectK.Common.Dtos.ScoreModule;

public sealed record KurinScoreDto
{
    public required Guid KurinKey { get; init; }
    public required ScoreAlgorithm Algorithm { get; init; }
    public required ScorePeriodDto Period { get; init; }
    public required ScorePeriodsDto Periods { get; init; }
    public required IReadOnlyList<ScoreGroupRowDto> Groups { get; init; }
    public required KurinScoreViewerDto Viewer { get; init; }
}
