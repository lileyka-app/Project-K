namespace ProjectK.Common.Dtos.ScoreModule;

public sealed record SheetGroupDto
{
    public required Guid GroupKey { get; init; }
    public required string GroupName { get; init; }
    public required bool CanScore { get; init; }
    public required IReadOnlyList<ScoreEntryDto> Entries { get; init; }
}
