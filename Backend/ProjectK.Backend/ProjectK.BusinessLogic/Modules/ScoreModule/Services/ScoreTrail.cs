using System.Text.Json;
using ProjectK.Common.Contracts.ScoreModule;
using ProjectK.Common.Entities.ScoreModule;

namespace ProjectK.BusinessLogic.Modules.ScoreModule.Services;

/// <summary>
/// Writes the trail a change to the score leaves: one event per change, with the row as it stands
/// afterwards. Static, like <c>DuesEntryTrail</c>; every score handler calls it before saving.
/// </summary>
public static class ScoreTrail
{
    public const string Created = "Created";
    public const string Updated = "Updated";
    public const string Deleted = "Deleted";

    public const string Attendance = "Attendance";
    public const string Entry = "Entry";
    public const string Rule = "Rule";
    public const string AttendanceRate = "AttendanceRate";
    public const string Item = "Item";
    public const string Stage = "Stage";
    public const string Settings = "Settings";
    public const string PrivateEntry = "PrivateEntry";
    public const string PrivateCriterion = "PrivateCriterion";

    /// <summary>Appends one event: <paramref name="row"/> is serialized as it stands now.</summary>
    public static void Record(
        IScoreUnitOfWork score,
        Guid kurinKey,
        string subject,
        Guid subjectKey,
        string action,
        object row,
        Guid? actorUserKey,
        DateTime nowUtc)
    {
        score.ScoreTrailEvents.Create(new ScoreTrailEvent
        {
            KurinKey = kurinKey,
            Subject = subject,
            SubjectKey = subjectKey,
            Action = action,
            ActorUserKey = actorUserKey,
            OccurredAtUtc = nowUtc,
            Snapshot = JsonSerializer.Serialize(row, row.GetType())
        });
    }
}
