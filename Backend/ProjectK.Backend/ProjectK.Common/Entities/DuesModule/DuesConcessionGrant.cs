using ProjectK.Common.Models.Dues;
using ProjectK.Common.Models.Enums;

namespace ProjectK.Common.Entities.DuesModule;

/// <summary>
/// A пільга of the край given to one membership for a run of quarters, with why it was given.
/// <para>
/// The край's rule is copied here when it is given, so a later change to the rule in the край's
/// profile does not rewrite what past quarters cost. Beside the kurin's own пільга
/// (<see cref="DuesConcession"/>) and the person's other grants, the cheapest one counts.
/// </para>
/// </summary>
public class DuesConcessionGrant : Entity
{
    public Guid DuesConcessionGrantKey { get; set; } = Guid.NewGuid();
    public Guid KurinKey { get; set; }
    public Guid MembershipKey { get; set; }

    /// <summary>The key of the пільга in the край's profile.</summary>
    public string ConcessionKey { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;
    public string Kind { get; set; } = string.Empty;
    public decimal Value { get; set; }
    public DuesParts Parts { get; set; }

    /// <summary>The first quarter it applies to, as <c>DuesQuarter.Index</c>.</summary>
    public int FromQuarter { get; set; }

    /// <summary>The last quarter it applies to; null while it has no end.</summary>
    public int? ToQuarter { get; set; }

    /// <summary>Why the person has it, such as «двоє дітей у курені».</summary>
    public string? Reason { get; set; }

    public Guid? SetByUserKey { get; set; }

    public bool AppliesTo(DuesQuarter quarter) =>
        FromQuarter <= quarter.Index && (ToQuarter is not { } to || quarter.Index <= to);

    public DuesConcessionRule Rule => new(ConcessionKey, Name, Kind, Value, Parts);
}
