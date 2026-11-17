using ProjectK.Common.Entities.Shared;

namespace ProjectK.Common.Entities.IdentityModule;

/// <summary>
/// One refresh token — that is, one signed-in session.
/// <para>
/// It used to be two columns on <c>AspNetUsers</c>, so an account could hold exactly one. Signing in
/// on a phone overwrote the desktop's token, and the desktop was thrown back to the login screen at
/// its next refresh with nothing to explain it. A row per session lets an account be signed in in
/// several places, and lets one of them be ended without touching the others.
/// </para>
/// </summary>
public class UserRefreshToken : Entity
{
    public Guid UserRefreshTokenKey { get; set; } = Guid.NewGuid();

    public Guid UserId { get; set; }

    /// <summary>The opaque token handed to the browser in its httpOnly cookie.</summary>
    public string Token { get; set; } = null!;

    public DateTime ExpiresAtUtc { get; set; }

    /// <summary>Set when the session ends — signed out, rotated, or revoked by a security change.</summary>
    public DateTime? RevokedAtUtc { get; set; }

    public AppUser User { get; set; } = null!;
}
