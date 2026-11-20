namespace ProjectK.Common.Entities.KurinModule.Planning;

public class PlanningParticipant
{
    public Guid PlanningParticipantKey { get; set; }
    public Guid PlanningSessionKey { get; set; }
    public PlanningSession PlanningSession { get; set; }

    public Guid MemberKey { get; set; }
    public string FullName { get; set; }
    public double RoleWeight { get; set; }

    public ICollection<ParticipantBusyRange> BusyRanges { get; set; } = new List<ParticipantBusyRange>();
}
