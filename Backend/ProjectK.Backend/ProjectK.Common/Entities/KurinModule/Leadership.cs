using ProjectK.Common.Models.Enums;

namespace ProjectK.Common.Entities.KurinModule;

public class Leadership
{
    public Guid LeadershipKey { get; set; }
    public LeadershipType Type { get; set; }
    public Guid? KurinKey { get; set; }
    public Guid? GroupKey { get; set; }
    public string? Name { get; set; }
    public DateOnly StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public ICollection<LeadershipHistory> LeadershipHistories { get; set; } = new List<LeadershipHistory>();

    public Kurin? Kurin { get; set; }
    public Group? Group { get; set; }
}
