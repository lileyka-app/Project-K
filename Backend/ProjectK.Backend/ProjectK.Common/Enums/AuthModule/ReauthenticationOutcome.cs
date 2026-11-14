namespace ProjectK.Common.Enums.AuthModule;

public enum ReauthenticationOutcome
{
    Verified,
    Wrong,

    /// <summary>Too many wrong passwords on this account lately; nothing was checked.</summary>
    Paused,

    /// <summary>A code was sent, but the account has no second factor to check it against.</summary>
    NoSecondFactor
}
