namespace ProjectK.Common.Models.Enums;

/// <summary>Why a calendar link stopped working. Stored as a number: never renumber.</summary>
public enum CalendarFeedRevokeReason
{
    /// <summary>A new link replaced it.</summary>
    Reissued = 1,

    /// <summary>Turned off by the person.</summary>
    Disabled = 2,

    /// <summary>A new password, email or second factor: anything that ends every session.</summary>
    SecurityChange = 3,

    /// <summary>The account was suspended.</summary>
    Suspended = 4
}
