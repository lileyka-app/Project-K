using ProjectK.Common.Entities;
using ProjectK.Common.Models.Enums;

namespace ProjectK.Common.Entities.ProbesAndBadgesModule;

public class BadgeProgress : Entity
{
    public Guid BadgeProgressKey { get; set; } = Guid.NewGuid();
    /// <summary>The person this progress belongs to. A key, not a relationship: the member lives
    /// in another module and is read through its contract.</summary>
    public Guid MemberKey { get; set; }
    public Guid KurinKey { get; set; }
    public string BadgeId { get; set; } = string.Empty;
    public BadgeProgressStatus Status { get; set; } = BadgeProgressStatus.Draft;
    public DateTime? SubmittedAtUtc { get; set; }
    public DateTime? ReviewedAtUtc { get; set; }
    public Guid? ReviewedByUserKey { get; set; }
    public string? ReviewedByName { get; set; }
    public string? ReviewedByRole { get; set; }
    public string? ReviewNote { get; set; }
}
