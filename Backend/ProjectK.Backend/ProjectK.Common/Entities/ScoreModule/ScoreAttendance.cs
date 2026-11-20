using ProjectK.Common.Entities.Shared;

namespace ProjectK.Common.Entities.ScoreModule;

/// <summary>
/// That a person was at one occurrence of an event. One fact, whoever marks it: the database holds
/// (membership, event, occurrence) unique among the marks still standing, so a гуртковий and a
/// курінний суддя cannot both mark it — the second sees it marked, and by whom.
/// <para>
/// An answer to the invitation never makes one of these; it only puts the person at the top of the
/// sheet. Taking a mark off keeps the row, marked removed, for the trail.
/// </para>
/// </summary>
public class ScoreAttendance : Entity
{
    public Guid ScoreAttendanceKey { get; set; } = Guid.NewGuid();
    public Guid KurinKey { get; set; }
    public Guid MembershipKey { get; set; }

    public Guid AgendaItemKey { get; set; }

    /// <summary>The start of the occurrence: what tells one сходини of a series from the next.</summary>
    public DateTime OccurrenceStartUtc { get; set; }

    public Guid? MarkedByUserKey { get; set; }
    public DateTime MarkedAtUtc { get; set; }

    public DateTime? RemovedAtUtc { get; set; }
    public Guid? RemovedByUserKey { get; set; }

    public bool IsRemoved => RemovedAtUtc.HasValue;
}
