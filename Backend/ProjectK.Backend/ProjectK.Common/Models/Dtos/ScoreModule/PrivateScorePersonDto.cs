namespace ProjectK.Common.Models.Dtos.ScoreModule;

/// <summary>One youth as the КВ sees them: the public points as the ground, the private ones on top.</summary>
public sealed record PrivateScorePersonDto
{
    public required Guid MembershipKey { get; init; }
    public required Guid MemberKey { get; init; }
    public required string FullName { get; init; }
    public required Guid? GroupKey { get; init; }
    public required string GroupName { get; init; }
    public required int PublicTotal { get; init; }
    public required int PrivateTotal { get; init; }
    public required IReadOnlyDictionary<Guid, int> ByCriterion { get; init; }
    /// <summary>What was written with no criterion.</summary>
    public required int Uncategorised { get; init; }
}
