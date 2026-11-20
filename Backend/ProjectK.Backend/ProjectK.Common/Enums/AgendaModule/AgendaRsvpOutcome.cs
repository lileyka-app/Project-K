namespace ProjectK.Common.Enums.AgendaModule;

/// <summary>How an answer to an event came out. Not stored.</summary>
public enum AgendaRsvpOutcome
{
    /// <summary>The answer is staged; the caller saves it.</summary>
    Staged,

    /// <summary>There is no such event.</summary>
    NoSuchEvent,

    /// <summary>The person could not see the event from its kurin's calendar.</summary>
    NotVisible,

    /// <summary>The event has no occurrence starting at the time given.</summary>
    NoSuchOccurrence
}
