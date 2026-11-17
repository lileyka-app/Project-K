using ProjectK.Common.Enums.KurinModule;

namespace ProjectK.Common.Entities.KurinModule;

/// <summary>
/// One person holding one role of a провід for a stretch of time. The person is named by key only:
/// they belong to the member module, and the relationship is configured without a navigation.
/// </summary>
public class LeadershipHistory
{
    public Guid LeadershipHistoryKey { get; set; }

    public Guid MemberKey { get; set; }

    public Guid LeadershipKey { get; set; }
    public Leadership Leadership { get; set; } = null!;

    public LeadershipRole Role { get; set; }

    public DateOnly StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
}
