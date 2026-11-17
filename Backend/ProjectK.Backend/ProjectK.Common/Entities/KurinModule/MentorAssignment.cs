using System.ComponentModel.DataAnnotations;
using ProjectK.Common.Entities.Shared;

namespace ProjectK.Common.Entities.KurinModule;

public class MentorAssignment : Entity
{
    [Key]
    public Guid MentorAssignmentKey { get; set; } = Guid.NewGuid();

    public Guid MentorUserKey { get; set; }

    /// <summary>The гурток. A key, not a relationship: it lives in another module.</summary>
    public Guid GroupKey { get; set; }

    public DateTime AssignedAtUtc { get; set; }
    public DateTime? RevokedAtUtc { get; set; }
}
