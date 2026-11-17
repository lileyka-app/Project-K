using ProjectK.Common.Dtos.MemberModule;
namespace ProjectK.Common.Dtos.KurinModule;

public class MentorAssignmentDto
{
    public Guid MentorAssignmentKey { get; set; }
    public Guid MentorUserKey { get; set; }
    public Guid GroupKey { get; set; }
    public string GroupName { get; set; } = string.Empty;
    public DateTime AssignedAtUtc { get; set; }
    public DateTime? RevokedAtUtc { get; set; }
    public MemberLookupDto? Member { get; set; }
}
