namespace ProjectK.Common.Dtos.DuesModule;

/// <summary>A пільга of the край given to a person, with its rule as it was when given.</summary>
public sealed record DuesConcessionGrantDto
{
    public required Guid GrantKey { get; init; }
    public required string ConcessionKey { get; init; }
    public required string Name { get; init; }
    public required string Kind { get; init; }
    public required decimal Value { get; init; }
    public required IReadOnlyList<string> AppliesTo { get; init; }
    public required QuarterDto FromQuarter { get; init; }
    public required QuarterDto? ToQuarter { get; init; }
    public required string? Reason { get; init; }
    public required bool IsActiveNow { get; init; }
}
