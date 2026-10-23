using MediatR;
using Microsoft.AspNetCore.Identity;
using ProjectK.BusinessLogic.Modules.AuthModule.Services;
using ProjectK.Common.Entities.AuthModule;
using ProjectK.Common.Interfaces.Modules.AuthModule;
using ProjectK.Common.Interfaces.Modules.InfrastructureModule;
using ProjectK.Common.Models.Dtos.AuthModule;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Records;

namespace ProjectK.BusinessLogic.Modules.AuthModule.Features.User.EnableMfa;

public class EnableMfaCommandHandler : IRequestHandler<EnableMfaCommand, ServiceResult<MfaEnableResponseDto>>
{
    private readonly UserManager<AppUser> _userManager;
    private readonly RefreshTokenInvalidation _sessions;
    private readonly ILoginResponseFactory _loginResponses;
    private readonly IActivityLogger _activityLogger;

    public EnableMfaCommandHandler(
        UserManager<AppUser> userManager,
        IActivityLogger activityLogger,
        RefreshTokenInvalidation sessions,
        ILoginResponseFactory loginResponses)
    {
        _userManager = userManager;
        _activityLogger = activityLogger;
        _sessions = sessions;
        _loginResponses = loginResponses;
    }

    public async Task<ServiceResult<MfaEnableResponseDto>> Handle(EnableMfaCommand request, CancellationToken cancellationToken)
    {
        var user = await _userManager.FindByIdAsync(request.UserKey.ToString());
        if (user == null)
        {
            return ServiceResult<MfaEnableResponseDto>.Failure(ResultType.Unauthorized, "Unauthorized", "User not found or unauthorized.");
        }

        var verificationCode = request.Code.Replace(" ", string.Empty).Replace("-", string.Empty);

        var isTokenValid = await _userManager.VerifyTwoFactorTokenAsync(
            user, _userManager.Options.Tokens.AuthenticatorTokenProvider, verificationCode);

        if (!isTokenValid)
        {
            return ServiceResult<MfaEnableResponseDto>.Failure(ResultType.BadRequest, "InvalidCode", "Invalid verification code.");
        }

        var enableResult = await _userManager.SetTwoFactorEnabledAsync(user, true);
        if (!enableResult.Succeeded)
        {
            return ServiceResult<MfaEnableResponseDto>.Failure(ResultType.BadRequest, "MfaSetupFailed", "Failed to enable MFA.");
        }

        var updateResult = await _userManager.UpdateAsync(user);
        if (!updateResult.Succeeded)
        {
            return ServiceResult<MfaEnableResponseDto>.Failure(ResultType.BadRequest, "MfaSetupFailed", "Failed to enable MFA.");
        }

        // Only once the change is stored. RevokeAllAsync commits on its own, so ending the
        // sessions first signed every device out even when this update failed.
        await _sessions.RevokeRefreshTokenAsync(user, cancellationToken);

        // The device that just enabled it keeps working: a session minted now carries the second
        // factor in its token, where the privileged-MFA gate reads it. The old access token said
        // «password only» and would have been refused on every save until it expired.
        var session = await _loginResponses.CreateAsync(user, cancellationToken);

        _activityLogger.LogAudit(
            action: "Account.MfaEnabled",
            actorUserId: user.Id,
            targetUserId: user.Id,
            reason: "User enabled MFA.");

        var recoveryCodes = await _userManager.GenerateNewTwoFactorRecoveryCodesAsync(user, 10);

        return new ServiceResult<MfaEnableResponseDto>(
            ResultType.Success,
            new MfaEnableResponseDto(true, recoveryCodes ?? Enumerable.Empty<string>(), session.Tokens));
    }
}
