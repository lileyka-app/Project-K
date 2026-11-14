using ProjectK.Common.Enums.MemberModule;


namespace ProjectK.Common.Entities.MemberModule;

public class LeadershipHistory
{
    public Guid LeadershipHistoryKey { get; set; }

    public Guid MemberKey { get; set; }
    public Member Member { get; set; } = null!;

    public Guid LeadershipKey { get; set; }
    public Leadership Leadership { get; set; } = null!;

    public LeadershipRole Role { get; set; }

    public DateOnly StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
}
