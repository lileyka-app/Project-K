using ProjectK.Common.Models.Enums;

namespace ProjectK.Common.Dtos.ScoreModule;

/// <summary>Everything the суддя куреня sets: how the kurin scores and what things are worth.</summary>
public sealed record KurinScoreSettingsDto
{
    public required Guid KurinKey { get; init; }
    public required ScoreAlgorithm Algorithm { get; init; }
    /// <summary>Every group of events, with what being at one of its events is worth (0 when nothing set).</summary>
    public required IReadOnlyList<ScoreAttendanceRateDto> AttendanceRates { get; init; }
    public required IReadOnlyList<ScoreRuleDto> Rules { get; init; }
    public required IReadOnlyList<ScoreItemDto> Items { get; init; }
    public required IReadOnlyList<ScoreStageDto> Stages { get; init; }
}
