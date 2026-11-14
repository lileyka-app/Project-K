namespace ProjectK.Common.Models.Dtos.ScoreModule;

/// <summary>One гурток's row in the kurin's table.</summary>
public sealed record ScoreGroupRowDto
{
    public required Guid GroupKey { get; init; }
    public required string GroupName { get; init; }
    public required int Place { get; init; }
    public required decimal Score { get; init; }
    public required decimal OtherScore { get; init; }
    public required int YouthPoints { get; init; }
    public required decimal YouthCount { get; init; }
    public required decimal Average { get; init; }
    public required int GroupPoints { get; init; }
    /// <summary>Whether the caller may open the гурток's own page.</summary>
    public required bool CanOpen { get; init; }
}
