using ProjectK.Common.Models.Enums;

namespace ProjectK.Common.Models.Dtos.ScoreModule;

public sealed record GroupScoreDto
{
    public required Guid GroupKey { get; init; }
    public required Guid KurinKey { get; init; }
    public required string GroupName { get; init; }
    public required ScoreAlgorithm Algorithm { get; init; }
    public required ScorePeriodDto Period { get; init; }
    public required ScorePeriodsDto Periods { get; init; }
    public required ScoreGroupRowDto Standing { get; init; }
    public required int GroupCount { get; init; }
    public required IReadOnlyList<ScorePersonRowDto> People { get; init; }
    public required IReadOnlyList<ScoreEntryDto> Entries { get; init; }
    public required IReadOnlyList<ScoreItemDto> Items { get; init; }
    public required bool CanScore { get; init; }
}
