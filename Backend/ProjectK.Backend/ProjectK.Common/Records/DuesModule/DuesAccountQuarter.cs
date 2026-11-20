namespace ProjectK.Common.Records.DuesModule;

/// <summary>One quarter of one account: what fell due, what of it is paid, and with what пільга.</summary>
public sealed record DuesAccountQuarter(
    DuesQuarter Quarter,
    DuesAmount Charged,
    DuesAmount Paid,
    bool IsConcession)
{
    /// <summary>The tag whose rate priced the quarter; null for the kurin's rate for everyone.</summary>
    public Guid? RateTagKey { get; init; }

    /// <summary>The пільга of the край that set the quarter's amount; null when none of them did.</summary>
    public string? ConcessionName { get; init; }

    /// <summary>Zero when paid up, negative while owed. Never positive: a surplus is the account's.</summary>
    public decimal Balance => Paid.Total - Charged.Total;
}
