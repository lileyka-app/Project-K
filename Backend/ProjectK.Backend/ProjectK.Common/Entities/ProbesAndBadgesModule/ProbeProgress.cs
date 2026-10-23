using ProjectK.Common.Entities;
using ProjectK.Common.Models.Enums;

namespace ProjectK.Common.Entities.ProbesAndBadgesModule;

public class ProbeProgress : Entity
{
    public Guid ProbeProgressKey { get; set; } = Guid.NewGuid();
    /// <summary>The person this progress belongs to. A key, not a relationship: the member lives
    /// in another module and is read through its contract.</summary>
    public Guid MemberKey { get; set; }
    public Guid KurinKey { get; set; }
    public string ProbeId { get; set; } = string.Empty;
    public ProbeProgressStatus Status { get; set; } = ProbeProgressStatus.NotStarted;
    public DateTime? CompletedAtUtc { get; set; }
    public Guid? CompletedByUserKey { get; set; }
    public string? CompletedByName { get; set; }
    public string? CompletedByRole { get; set; }
    public DateTime? VerifiedAtUtc { get; set; }
    public Guid? VerifiedByUserKey { get; set; }
    public string? VerifiedByName { get; set; }
    public string? VerifiedByRole { get; set; }
}
