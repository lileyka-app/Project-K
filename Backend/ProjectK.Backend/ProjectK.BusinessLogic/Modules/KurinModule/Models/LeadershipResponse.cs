using ProjectK.Common.Models.Dtos.KurinModule;
using ProjectK.Common.Models.Enums;

namespace ProjectK.BusinessLogic.Modules.KurinModule.Models;

public class LeadershipResponse
{
    public Guid LeadershipKey { get; set; }
    public LeadershipType Type { get; set; }
    public Guid EntityKey { get; set; }
    public Guid? KurinKey { get; set; }
    public Guid? GroupKey { get; set; }
    public IEnumerable<LeadershipHistoryMemberDto> LeadershipHistories { get; set; } = new List<LeadershipHistoryMemberDto>();
    public DateOnly StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
}
