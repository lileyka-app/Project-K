namespace ProjectK.Common.Models.Dtos.ScoreModule;

public sealed record ScoreStageDto
{
    public required Guid ScoreStageKey { get; init; }
    public required string Name { get; init; }
    public required DateOnly FromDate { get; init; }
    public required DateOnly ToDate { get; init; }
}
