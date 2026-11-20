using ProjectK.Common.Entities.Shared;

namespace ProjectK.Common.Entities.ScoreModule;

/// <summary>
/// One change to the score, and what the changed row looked like afterwards: a mark, an entry, a rule,
/// a position, a stage. Append-only and held by key, not by relationship, so one table serves them all
/// and a trail outlives what it describes.
/// </summary>
public class ScoreTrailEvent : Entity
{
    public Guid ScoreTrailEventKey { get; set; } = Guid.NewGuid();
    public Guid KurinKey { get; set; }

    /// <summary>The kind of row: <c>Attendance</c>, <c>Entry</c>, <c>Rule</c>, <c>AttendanceRate</c>, <c>Item</c>, <c>Stage</c>, <c>Settings</c>.</summary>
    public string Subject { get; set; } = string.Empty;
    public Guid SubjectKey { get; set; }

    /// <summary><c>Created</c>, <c>Updated</c>, <c>Deleted</c>.</summary>
    public string Action { get; set; } = string.Empty;

    public Guid? ActorUserKey { get; set; }
    public DateTime OccurredAtUtc { get; set; }

    /// <summary>The row as it stood after the change, as JSON.</summary>
    public string Snapshot { get; set; } = string.Empty;
}
