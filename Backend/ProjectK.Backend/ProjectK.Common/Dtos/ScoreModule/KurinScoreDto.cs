using ProjectK.Common.Enums.ScoreModule;

namespace ProjectK.Common.Dtos.ScoreModule;

public sealed record KurinScoreDto
{
    public required Guid KurinKey { get; init; }
    public required ScoreAlgorithm Algorithm { get; init; }
    public required ScorePeriodDto Period { get; init; }
    public required ScorePeriodsDto Periods { get; init; }
    public required IReadOnlyList<ScoreGroupRowDto> Groups { get; init; }

    /// <summary>False for a senior kurin: it has no гуртки, so the table is of people, <see cref="People"/>.</summary>
    public required bool HasGroups { get; init; }

    /// <summary>Each person's points, best first; filled only where the kurin has no гуртки.</summary>
    public required IReadOnlyList<ScorePersonRowDto> People { get; init; }

    public required KurinScoreViewerDto Viewer { get; init; }
}
