namespace ProjectK.Common.Enums.KurinModule;

/// <summary>Why an event is on the viewer's calendar.</summary>
public enum AgendaAudience
{
    /// <summary>It is aimed at them, or they raised it: theirs to answer, follow or edit.</summary>
    Assigned = 0,

    /// <summary>Only because its group is «графік куреня»: there to see, not to answer.</summary>
    Schedule = 1
}
