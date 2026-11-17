namespace ProjectK.Common.Records.DuesModule;

/// <summary>What is in a box, and how much of it is the box's own to spend.</summary>
public sealed record DuesCashBox(decimal Cash, decimal Card, decimal ToForward)
{
    public decimal Total => Cash + Card;

    /// <summary>
    /// What people have paid for quarters not charged yet, in a kurin's own box: it is theirs until the
    /// quarters come, and then partly the станиця's and the край's.
    /// </summary>
    public decimal Ahead { get; init; }

    /// <summary>Total less what has to go up and what is paid ahead — the money the box may spend.</summary>
    public decimal Own => Total - ToForward - Ahead;
}
