using ProjectK.Common.Models.Enums;

namespace ProjectK.Common.Dtos.ScoreModule;

public sealed record ScorePersonRowDto
{
    public required Guid MembershipKey { get; init; }
    public required Guid MemberKey { get; init; }
    public required string FullName { get; init; }
    /// <summary><c>Current</c> in the гурток, <c>Moved</c> to another, or <c>Left</c> the kurin.</summary>
    public required string Standing { get; init; }
    public required int Total { get; init; }
    public required IReadOnlyDictionary<ScoreSource, int> BySource { get; init; }
}
