namespace ProjectK.BusinessLogic.Modules.ProbesAndBadgesModule.Services;

/// <summary>Whether a probe nobody has started for a person yet may be started now.</summary>
public enum ProbeStart
{
    Allowed,

    /// <summary>The probe is not one this person walks: another edition, or not in the catalogue.</summary>
    NotInLine,

    /// <summary>The probe before it in the line is not closed yet.</summary>
    WaitsForPrevious
}
