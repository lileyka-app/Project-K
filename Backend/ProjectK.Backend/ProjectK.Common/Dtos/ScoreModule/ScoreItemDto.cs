namespace ProjectK.Common.Dtos.ScoreModule;

public sealed record ScoreItemDto
{
    public required Guid ScoreItemKey { get; init; }
    public required string Name { get; init; }
    public required int Points { get; init; }
    public required bool IsArchived { get; init; }
}
