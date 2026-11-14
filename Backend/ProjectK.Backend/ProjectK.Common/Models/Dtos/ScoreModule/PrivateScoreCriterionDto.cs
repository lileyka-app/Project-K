namespace ProjectK.Common.Models.Dtos.ScoreModule;

public sealed record PrivateScoreCriterionDto
{
    public required Guid PrivateScoreCriterionKey { get; init; }
    public required string Name { get; init; }
    public required bool IsArchived { get; init; }
}
