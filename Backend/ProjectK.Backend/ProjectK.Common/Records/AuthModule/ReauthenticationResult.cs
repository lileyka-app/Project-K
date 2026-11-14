using ProjectK.Common.Enums.AuthModule;

namespace ProjectK.Common.Records.AuthModule;

/// <summary>
/// How the account proved itself, and the stamp a grant made from it is bound to. A verified result
/// also names the account, for whoever is told about what it did: an administrator has no member card.
/// </summary>
public sealed record ReauthenticationResult(ReauthenticationOutcome Outcome, ReauthenticationMethod Method, string? SecurityStamp = null, string? AccountName = null)
{
    public bool Succeeded => Outcome == ReauthenticationOutcome.Verified;
}
