namespace ProjectK.Common.Entities.DuesModule;

/// <summary>
/// One quarter of вкладка owed by one membership, and where the person stood when it fell due.
/// <para>
/// The amount is not stored — it comes from the rates when the ledger is read. What is stored is the
/// гурток: a membership keeps only its current one, and a debt's гурткова частина stays with the
/// гурток it arose in after the person moves. A senior kurin has no гуртки, so its charges have none
/// and the account is the person's own in the kurin's box.
/// </para>
/// </summary>
public class DuesCharge : Entity
{
    public Guid DuesChargeKey { get; set; } = Guid.NewGuid();
    public Guid KurinKey { get; set; }
    /// <summary>The гурток the person stood in; null for a personal account in the kurin's box.</summary>
    public Guid? GroupKey { get; set; }
    public Guid MembershipKey { get; set; }
    /// <summary>As <c>DuesQuarter.Index</c>. One charge per membership per quarter.</summary>
    public int Quarter { get; set; }
    /// <summary>
    /// The tag whose rate the quarter is priced at; null for the kurin's rate for everyone. Kept here
    /// because a person's tags have no history: a quarter paid in full keeps it for good, while one not
    /// paid in full is set again when the kurin's rates or the person's tags change (DuesAccrual).
    /// </summary>
    public Guid? RateTagKey { get; set; }
}
