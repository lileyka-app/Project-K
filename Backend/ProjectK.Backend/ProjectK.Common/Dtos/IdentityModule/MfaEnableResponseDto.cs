namespace ProjectK.Common.Dtos.IdentityModule;

/// <summary>
/// What enabling the second factor answers with. <paramref name="Tokens"/> is the session that
/// replaces the ones just ended: minted after the change, so its <c>amr</c> already says so and the
/// browser carries on without signing in again.
/// </summary>
public record MfaEnableResponseDto(bool Enabled, IEnumerable<string> RecoveryCodes, JwtResponse? Tokens = null);
