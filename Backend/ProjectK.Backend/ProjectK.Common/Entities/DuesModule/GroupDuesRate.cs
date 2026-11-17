using ProjectK.Common.Entities.Shared;

namespace ProjectK.Common.Entities.DuesModule;

/// <summary>
/// The гурток's own part of a quarterly вкладка, from one quarter until the next change. Each гурток
/// sets it for itself, on top of the kurin's станиця and kurin parts.
/// </summary>
public class GroupDuesRate : Entity
{
    public Guid GroupDuesRateKey { get; set; } = Guid.NewGuid();
    public Guid KurinKey { get; set; }
    public Guid GroupKey { get; set; }

    /// <summary>The first quarter it applies to, as <c>DuesQuarter.Index</c>.</summary>
    public int FromQuarter { get; set; }

    public decimal GroupShare { get; set; }

    public Guid? SetByUserKey { get; set; }
}
