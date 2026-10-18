namespace ProjectK.Common.Models.Dues;

/// <summary>A quarter's вкладка, split by where the money goes. The край part is last so a kurin without one reads as before.</summary>
public sealed record DuesAmount(decimal Stanytsia, decimal Kurin, decimal Group, decimal Krai = 0)
{
    public static DuesAmount Zero { get; } = new(0, 0, 0);

    public decimal Total => Stanytsia + Krai + Kurin + Group;

    /// <summary>What the гурток hands up to the kurin: everything but its own part.</summary>
    public decimal Up => Stanytsia + Krai + Kurin;
}

/// <summary>One quarter of one account: what fell due, what of it is paid, and with what пільга.</summary>
public sealed record DuesAccountQuarter(
    DuesQuarter Quarter,
    DuesAmount Charged,
    DuesAmount Paid,
    bool IsConcession)
{
    /// <summary>Zero when paid up, negative while owed. Never positive: a surplus is the account's.</summary>
    public decimal Balance => Paid.Total - Charged.Total;
}

/// <summary>
/// One membership's вкладка in one гурток. A person moved between гуртки has one per гурток: the
/// гурткова частина of an old debt stays with the гурток it arose in.
/// </summary>
public sealed record DuesAccount(
    Guid MembershipKey,
    Guid GroupKey,
    IReadOnlyList<DuesAccountQuarter> Quarters,
    decimal Payments)
{
    public decimal Charged => Quarters.Sum(q => q.Charged.Total);

    /// <summary>Payments less charges: negative is debt, positive a surplus that pays what comes next.</summary>
    public decimal Balance => Payments - Charged;

    public DuesAmount Allocated => new(
        Quarters.Sum(q => q.Paid.Stanytsia),
        Quarters.Sum(q => q.Paid.Kurin),
        Quarters.Sum(q => q.Paid.Group),
        Quarters.Sum(q => q.Paid.Krai));
}

/// <summary>What is in a box, and how much of it is the box's own to spend.</summary>
public sealed record DuesCashBox(decimal Cash, decimal Card, decimal ToForward)
{
    public decimal Total => Cash + Card;

    /// <summary>Total less what has to go up — the money the box may spend.</summary>
    public decimal Own => Total - ToForward;
}

/// <summary>A гурток's box, and the transfers to the kurin that have not been confirmed yet.</summary>
public sealed record GroupDuesBox(Guid GroupKey, DuesCashBox Box, decimal InTransit);

/// <summary>How one гурток stands with the kurin: what it owes up, what it handed over, what arrived.</summary>
public sealed record GroupHandover(
    Guid GroupKey,
    decimal OwedUp,
    decimal Transferred,
    decimal Received)
{
    /// <summary>Collected for the станиця and the kurin but not handed over yet.</summary>
    public decimal Outstanding => OwedUp - Transferred;
}

/// <summary>
/// The kurin's own box and how each гурток stands with it. <see cref="DuesCashBox.ToForward"/> is
/// <see cref="ToStanytsia"/> and <see cref="ToKrai"/> together.
/// </summary>
public sealed record KurinDuesBox(DuesCashBox Box, IReadOnlyList<GroupHandover> Handovers, decimal ToStanytsia = 0, decimal ToKrai = 0);
