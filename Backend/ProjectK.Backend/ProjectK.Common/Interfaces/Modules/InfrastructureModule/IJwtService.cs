using ProjectK.Common.Models.Dtos.AuthModule;
using ProjectK.Common.Models.Records;

namespace ProjectK.Common.Interfaces.Modules.InfrastructureModule;

public interface IJwtService
{
    /// <summary>
    /// What the kurin's archive holds while its danger zone is on: proof that this account proved
    /// itself again, for this kurin, until <paramref name="expiresUtc"/>. Its own audience, so it is
    /// never an access token, and it carries the security stamp so a password change voids it.
    /// </summary>
    string GenerateArchiveDangerTicket(Guid userId, Guid kurinKey, Guid ticketId, string securityStamp, DateTime expiresUtc);

    /// <summary>The ticket read back, or <c>null</c> when it is not one, or expired.</summary>
    ArchiveDangerTicket? ReadArchiveDangerTicket(string token);

    /// <summary>
    /// The token a request carries. <paramref name="hasSecondFactor"/> is written into it as
    /// <c>amr</c>, so the privileged-MFA gate can answer from the token alone.
    /// </summary>
    string GenerateAccessToken(string userId, string email, IEnumerable<string> roles, string? kurinKey, bool hasSecondFactor);

    RefreshToken GenerateRefreshToken();

    /// <summary>
    /// A short-lived proof that the password step passed for this account. The second-factor
    /// step requires it, so a code on its own never completes a sign-in.
    /// </summary>
    string GenerateMfaChallengeToken(Guid userId);

    /// <summary>
    /// The account a challenge was minted for, or <c>null</c> when the token is invalid, expired,
    /// or was issued for another purpose — an access token is not a challenge.
    /// </summary>
    Guid? ReadMfaChallenge(string token);

    /// <summary>
    /// What the dev role switcher hands the browser when an administrator steps into another
    /// account, so that stepping back out needs no password. Twelve hours, its own audience,
    /// never accepted as an access token. Issued only where the switcher exists.
    /// </summary>
    string GenerateDevReturnTicket(Guid userId);

    /// <summary>The administrator a return ticket was issued to, or <c>null</c> when it is not one, or expired.</summary>
    Guid? ReadDevReturnTicket(string token);

    /// <summary>
    /// What a device keeps after finishing the second factor, so that the next sign-ins skip it.
    /// Lives <c>Security:MfaTrustDays</c> (seven by default) and carries the account's security
    /// stamp: a password change or an MFA reset rotates the stamp and voids the trust.
    /// </summary>
    MfaTrustGrant GenerateMfaTrustToken(Guid userId, string securityStamp);

    /// <summary>The account and stamp a trust token was issued for, or <c>null</c> when it is not one, or expired.</summary>
    MfaTrust? ReadMfaTrust(string token);
}
