namespace ProjectK.Common.Enums.ScoreModule;

/// <summary>
/// Where a point came from. The table of a гурток breaks a youth's points down by these, so a new
/// source is a new column there — append, never renumber: the number is stored with every rule.
/// </summary>
public enum ScoreSource
{
    /// <summary>A mark that the person was at an event.</summary>
    Attendance = 0,

    /// <summary>A position from the kurin's list — «однострій», «запізнення» — given by hand.</summary>
    Item = 1,

    /// <summary>A free entry: an amount and a reason, given by hand.</summary>
    Free = 2,

    /// <summary>A вмілість confirmed.</summary>
    Skill = 3,

    /// <summary>A point of a проба signed.</summary>
    ProbePoint = 4,

    /// <summary>A whole проба closed.</summary>
    Probe = 5,

    /// <summary>A quarter of вкладка closed with no debt.</summary>
    Dues = 6,

    /// <summary>A пересторога in force. Its rule is negative and depends on the level.</summary>
    Warning = 7
}
