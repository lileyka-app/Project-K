namespace ProjectK.Common.Models.Dtos.AuthModule;

/// <summary>
/// A rotated session and what it may do now. <see cref="Access"/> is additive: a client that
/// reads only the tokens keeps working, one that reads it picks up a change without signing in again.
/// </summary>
public sealed class RefreshTokenResponse : JwtResponse
{
    public AccessSnapshot Access { get; set; } = new();
}
