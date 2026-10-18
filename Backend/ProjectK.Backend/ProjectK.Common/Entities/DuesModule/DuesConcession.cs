namespace ProjectK.Common.Entities.DuesModule;

/// <summary>
/// A пільга switched on or off for one membership from one quarter. The rows are the history; the
/// latest one at or before a quarter says whether that quarter is charged at the пільгові amounts of
/// the kurin's статті.
/// </summary>
public class DuesConcession : Entity
{
    public Guid DuesConcessionKey { get; set; } = Guid.NewGuid();
    public Guid KurinKey { get; set; }
    public Guid MembershipKey { get; set; }

    /// <summary>The first quarter it applies to, as <c>DuesQuarter.Index</c>.</summary>
    public int FromQuarter { get; set; }

    public bool IsConcession { get; set; }

    public Guid? SetByUserKey { get; set; }
}
