namespace ProjectK.Common.Records.DuesModule;

/// <summary>
/// One membership's вкладка in one гурток. A person moved between гуртки has one per гурток: the
/// гурткова частина of an old debt stays with the гурток it arose in.
/// </summary>
public sealed record DuesAccount(
    Guid MembershipKey,
    Guid? GroupKey,
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
