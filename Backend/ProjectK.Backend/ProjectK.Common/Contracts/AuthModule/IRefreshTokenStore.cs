using ProjectK.Common.Entities.AuthModule;

namespace ProjectK.Common.Contracts.AuthModule;

/// <summary>
/// The signed-in sessions of an account. Declared here so BusinessLogic can hand out and end sessions
/// without knowing they are rows in a table.
/// </summary>
public interface IRefreshTokenStore
{
    /// <summary>Records a new session. Existing ones are left alone — that is the point.</summary>
    Task IssueAsync(Guid userId, string token, DateTime expiresAtUtc, CancellationToken cancellationToken = default);

    /// <summary>The session this token belongs to, if it is neither revoked nor expired.</summary>
    Task<UserRefreshToken?> FindActiveAsync(string token, CancellationToken cancellationToken = default);

    /// <summary>
    /// Ends one session, leaving the account's other sessions signed in. Answers whether this call is
    /// the one that ended it.
    /// <para>
    /// The answer matters for rotation: two refreshes racing on the same token both find it active,
    /// and without a way to tell who actually spent it both would mint a session. Only the caller
    /// that gets <c>true</c> may issue a replacement.
    /// </para>
    /// </summary>
    Task<bool> RevokeAsync(string token, CancellationToken cancellationToken = default);

    /// <summary>
    /// Ends every session an account holds. For the changes that must not leave an old session alive:
    /// a new password, a confirmed email change, anything touching the second factor.
    /// </summary>
    Task RevokeAllAsync(Guid userId, CancellationToken cancellationToken = default);
}
