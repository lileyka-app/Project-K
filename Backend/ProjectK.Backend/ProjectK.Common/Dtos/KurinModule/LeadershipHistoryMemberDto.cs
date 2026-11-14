namespace ProjectK.Common.Dtos.KurinModule;

public class LeadershipHistoryMemberDto
{
    public Guid? LeadershipHistoryKey { get; set; }
    public Guid? LeadershipKey { get; set; }
    public string Role { get; set; }
    public DateOnly StartDate { get; set; }
    public DateOnly? EndDate { get; set; }

    // Member Details
    public MemberLookupDto Member { get; set; }
}
