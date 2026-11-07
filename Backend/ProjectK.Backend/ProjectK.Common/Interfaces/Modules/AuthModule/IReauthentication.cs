namespace ProjectK.Common.Interfaces.Modules.AuthModule;

/// <summary>
/// Asks a signed-in account to prove itself again before something it cannot take back. The same
/// checks sign-in makes, on the same counters: a wrong password counts towards the account's
/// sign-in pause, and a code is the authenticator's or a recovery code.
/// </summary>
public interface IReauthentication
{
    Task<ReauthenticationResult> VerifyAsync(Guid userKey, string? password, string? code, CancellationToken cancellationToken = default);

    /// <summary>The account's current security stamp; null when the account is gone.</summary>
    Task<string?> GetSecurityStampAsync(Guid userKey, CancellationToken cancellationToken = default);
}

/// <summary>
/// How the account proved itself, and the stamp a grant made from it is bound to. A verified result
/// also names the account, for whoever is told about what it did: an administrator has no member card.
/// </summary>
public sealed record ReauthenticationResult(ReauthenticationOutcome Outcome, ReauthenticationMethod Method, string? SecurityStamp = null, string? AccountName = null)
{
    public bool Succeeded => Outcome == ReauthenticationOutcome.Verified;
}

public enum ReauthenticationOutcome
{
    Verified,
    Wrong,

    /// <summary>Too many wrong passwords on this account lately; nothing was checked.</summary>
    Paused,

    /// <summary>A code was sent, but the account has no second factor to check it against.</summary>
    NoSecondFactor
}

public enum ReauthenticationMethod
{
    Password,
    Code
}
