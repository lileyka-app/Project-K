namespace ProjectK.Common.Dtos.DuesModule;

public sealed record DuesAccountQuarterDto
{
    public required QuarterDto Quarter { get; init; }
    public required DuesAmountDto Charged { get; init; }
    public required DuesAmountDto Paid { get; init; }
    public required decimal Balance { get; init; }
    public required bool IsConcession { get; init; }

    /// <summary>The пільга of the край that set this quarter's amount, if one did.</summary>
    public required string? ConcessionName { get; init; }
}
