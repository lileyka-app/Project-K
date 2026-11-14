using ProjectK.Common.Entities.KurinModule;
using System.ComponentModel.DataAnnotations;
using ProjectK.Common.Entities.Shared;

namespace ProjectK.Common.Entities.MemberModule;

public class MentorAssignment : Entity
{
    [Key]
    public Guid MentorAssignmentKey { get; set; } = Guid.NewGuid();

    public Guid MentorUserKey { get; set; }

    public Guid GroupKey { get; set; }
    public Group Group { get; set; } = null!;

    public DateTime AssignedAtUtc { get; set; }
    public DateTime? RevokedAtUtc { get; set; }
}
