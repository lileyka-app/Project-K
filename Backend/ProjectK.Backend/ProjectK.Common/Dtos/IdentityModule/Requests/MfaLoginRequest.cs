namespace ProjectK.Common.Dtos.IdentityModule.Requests;

/// <summary>
/// The second step of a sign-in. <paramref name="MfaToken"/> is what the password step answered
/// with; without it a code alone is refused.
/// </summary>
public record MfaLoginRequest(string Email, string Code, bool RememberMe, string? MfaToken = null);
