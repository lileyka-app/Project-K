using ProjectK.Common.Entities.Shared;
using ProjectK.Common.Enums.ScoreModule;

namespace ProjectK.Common.Entities.ScoreModule;

/// <summary>
/// What one automatic source is worth, from a date until the next change — a вмілість, a signed point
/// of a проба, a quarter of вкладка paid, a пересторога. Like a dues rate: a change does not rewrite
/// what was earned before it. Zero turns the source off.
/// <para>
/// Attendance is not here: what an event is worth lives in <see cref="ScoreAttendanceRate"/>, and is
/// current rather than dated, because a суддя fixing it means to fix the past too.
/// </para>
/// </summary>
public class ScoreRule : Entity
{
    public Guid ScoreRuleKey { get; set; } = Guid.NewGuid();
    public Guid KurinKey { get; set; }

    public ScoreSource Source { get; set; }

    /// <summary>Which kind within the source: the level for <see cref="ScoreSource.Warning"/>, 0 otherwise.</summary>
    public int Variant { get; set; }

    /// <summary>The first day it applies to.</summary>
    public DateOnly FromDate { get; set; }

    /// <summary>Negative for a пересторога.</summary>
    public int Points { get; set; }

    public Guid? SetByUserKey { get; set; }
}
