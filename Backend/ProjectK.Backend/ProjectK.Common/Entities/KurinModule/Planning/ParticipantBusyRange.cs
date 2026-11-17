namespace ProjectK.Common.Entities.KurinModule.Planning;

public class ParticipantBusyRange
{
    public Guid ParticipantBusyRangeKey { get; set; }
    public Guid PlanningParticipantKey { get; set; }
    public PlanningParticipant PlanningParticipant { get; set; }
    public DateTime Start { get; set; }
    public DateTime End { get; set; }
}
