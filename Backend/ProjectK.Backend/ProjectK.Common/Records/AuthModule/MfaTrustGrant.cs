namespace ProjectK.Common.Records.AuthModule;

/// <summary>
/// What a browser keeps after finishing the second factor: proof that this device already did,
/// so the next sign-ins skip it until <paramref name="ExpiresUtc"/>. Bound to the account's
/// security stamp, so a password change or an MFA reset ends every device's trust at once.
/// </summary>
public sealed record MfaTrustGrant(string Token, DateTime ExpiresUtc);
