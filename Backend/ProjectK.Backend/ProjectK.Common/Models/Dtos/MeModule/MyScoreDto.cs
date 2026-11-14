using ProjectK.Common.Models.Enums;

namespace ProjectK.Common.Models.Dtos.MeModule;

/// <summary>The person's own points this пластовий рік in one kurin, and where their гурток stands.</summary>
public sealed record MyScoreDto
{
    public required MyKurinRefDto Kurin { get; init; }
    public required Guid GroupKey { get; init; }
    public required string GroupName { get; init; }
    public required string PeriodLabel { get; init; }
    public required int Total { get; init; }
    public required IReadOnlyDictionary<ScoreSource, int> BySource { get; init; }
    public required int GroupPlace { get; init; }
    public required int GroupCount { get; init; }
    public required decimal GroupScore { get; init; }
    public required ScoreAlgorithm Algorithm { get; init; }
}
