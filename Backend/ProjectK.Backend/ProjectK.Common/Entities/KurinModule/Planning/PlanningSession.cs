namespace ProjectK.Common.Entities.KurinModule.Planning;

public class PlanningSession
{
    public Guid PlanningSessionKey { get; set; }
    public Guid KurinKey { get; set; }
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Who raised the session. Null on rows created before authorship was tracked — those fall back
    /// to kurin-wide grants, since an "own planning" rule has nobody to match against.
    /// </summary>
    public Guid? CreatedByUserKey { get; set; }

    public DateTime SearchStart { get; set; }
    public DateTime SearchEnd { get; set; }
    public int DurationDays { get; set; }

    public DateTime? OptimalStartDate { get; set; }
    public DateTime? OptimalEndDate { get; set; }
    public double ConflictScore { get; set; }
    public bool IsCalculated { get; set; }

    public Kurin Kurin { get; set; }
    public ICollection<PlanningParticipant> Participants { get; set; } = new List<PlanningParticipant>();
}
