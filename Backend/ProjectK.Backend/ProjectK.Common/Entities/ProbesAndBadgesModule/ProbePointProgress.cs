using ProjectK.Common.Entities.Shared;

namespace ProjectK.Common.Entities.ProbesAndBadgesModule;

public class ProbePointProgress : Entity
{
    public Guid ProbePointProgressKey { get; set; } = Guid.NewGuid();
    /// <summary>The person this progress belongs to. A key, not a relationship: the member lives
    /// in another module and is read through its contract.</summary>
    public Guid MemberKey { get; set; }
    public Guid KurinKey { get; set; }
    public string ProbeId { get; set; } = string.Empty;
    public string PointId { get; set; } = string.Empty;
    public bool IsSigned { get; set; }
    public DateTime? SignedAtUtc { get; set; }
    public Guid? SignedByUserKey { get; set; }
    public string? SignedByName { get; set; }
    public string? SignedByRole { get; set; }
}
