using ProjectK.Common.Records.AuthModule;

namespace ProjectK.Common.Contracts.AuthModule;

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
