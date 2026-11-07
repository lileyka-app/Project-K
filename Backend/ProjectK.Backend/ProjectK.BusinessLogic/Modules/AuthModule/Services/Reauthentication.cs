using Microsoft.AspNetCore.Identity;
using ProjectK.Common.Entities.AuthModule;
using ProjectK.Common.Interfaces.Modules.AuthModule;

namespace ProjectK.BusinessLogic.Modules.AuthModule.Services;

/// <summary>
/// The checks of sign-in, asked again of an account that is already in. Wrong answers count in
/// <see cref="ILoginThrottle"/> like a sign-in's, so a session left open is no way around the
/// pause; a code is the authenticator's or a recovery code, as on <c>mfa/login-verify</c>.
/// </summary>
public sealed class Reauthentication : IReauthentication
{
    private readonly UserManager<AppUser> _userManager;
    private readonly ILoginThrottle _throttle;

    public Reauthentication(UserManager<AppUser> userManager, ILoginThrottle throttle)
    {
        _userManager = userManager;
        _throttle = throttle;
    }

    public async Task<ReauthenticationResult> VerifyAsync(Guid userKey, string? password, string? code, CancellationToken cancellationToken = default)
    {
        var method = string.IsNullOrWhiteSpace(code) ? ReauthenticationMethod.Password : ReauthenticationMethod.Code;
        var user = await _userManager.FindByIdAsync(userKey.ToString());
        if (user is null)
        {
            return new ReauthenticationResult(ReauthenticationOutcome.Wrong, method);
        }

        // Codes count towards the same pause: six digits are guessed faster than a password.
        var email = user.Email ?? user.Id.ToString();
        if (_throttle.IsPaused(email))
        {
            return new ReauthenticationResult(ReauthenticationOutcome.Paused, method);
        }

        var result = method == ReauthenticationMethod.Password
            ? await VerifyPasswordAsync(user, password)
            : await VerifyCodeAsync(user, code!);
        if (result.Outcome == ReauthenticationOutcome.Wrong)
        {
            _throttle.RecordFailure(email);
        }
        else if (result.Succeeded && method == ReauthenticationMethod.Password)
        {
            _throttle.Reset(email);
        }

        return result;
    }

    public async Task<string?> GetSecurityStampAsync(Guid userKey, CancellationToken cancellationToken = default) =>
        (await _userManager.FindByIdAsync(userKey.ToString()))?.SecurityStamp;

    private async Task<ReauthenticationResult> VerifyPasswordAsync(AppUser user, string? password) =>
        string.IsNullOrEmpty(password) || !await _userManager.CheckPasswordAsync(user, password)
            ? new ReauthenticationResult(ReauthenticationOutcome.Wrong, ReauthenticationMethod.Password)
            : new ReauthenticationResult(ReauthenticationOutcome.Verified, ReauthenticationMethod.Password, user.SecurityStamp ?? string.Empty, NameOf(user));

    private async Task<ReauthenticationResult> VerifyCodeAsync(AppUser user, string code)
    {
        if (!user.TwoFactorEnabled)
        {
            return new ReauthenticationResult(ReauthenticationOutcome.NoSecondFactor, ReauthenticationMethod.Code);
        }

        var verificationCode = code.Replace(" ", string.Empty).Replace("-", string.Empty);
        var valid = await _userManager.VerifyTwoFactorTokenAsync(user, _userManager.Options.Tokens.AuthenticatorTokenProvider, verificationCode)
                    || (await _userManager.RedeemTwoFactorRecoveryCodeAsync(user, code.Trim())).Succeeded;

        return valid
            ? new ReauthenticationResult(ReauthenticationOutcome.Verified, ReauthenticationMethod.Code, user.SecurityStamp ?? string.Empty, NameOf(user))
            : new ReauthenticationResult(ReauthenticationOutcome.Wrong, ReauthenticationMethod.Code);
    }

    private static string? NameOf(AppUser user)
    {
        var name = $"{user.FirstName} {user.LastName}".Trim();
        return name.Length > 0 ? name : null;
    }
}
