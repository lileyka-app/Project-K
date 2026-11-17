using MediatR;
using Microsoft.AspNetCore.Identity;
using ProjectK.BusinessLogic.Modules.IdentityModule.Services;
using ProjectK.Common.Contracts.IdentityModule;
using ProjectK.Common.Entities.IdentityModule;
using ProjectK.Common.Enums.Shared;
using ProjectK.Common.Records.Shared;

using ProjectK.Common.Rules.IdentityModule;

namespace ProjectK.BusinessLogic.Modules.IdentityModule.Features.Mfa;

public sealed record ResetOwnMfaCommand(Guid UserKey, string CurrentPassword) : IRequest<ServiceResult<Unit>>;

public sealed class ResetOwnMfaCommandHandler : IRequestHandler<ResetOwnMfaCommand, ServiceResult<Unit>>
{
    private readonly UserManager<AppUser> _userManager;
    private readonly RefreshTokenInvalidation _sessions;
    private readonly IActivityLogger _activityLogger;

    public ResetOwnMfaCommandHandler(
        UserManager<AppUser> userManager,
        IActivityLogger activityLogger,
        RefreshTokenInvalidation sessions)
    {
        _userManager = userManager;
        _activityLogger = activityLogger;
        _sessions = sessions;
    }

    public async Task<ServiceResult<Unit>> Handle(ResetOwnMfaCommand request, CancellationToken cancellationToken)
    {
        var user = await _userManager.FindSignedInAsync(request.UserKey);
        if (user == null)
        {
            return IdentityErrors.AccountMissing<Unit>();
        }

        if (string.IsNullOrWhiteSpace(request.CurrentPassword))
        {
            return ServiceResult<Unit>.Failure(ResultType.Unauthorized, IdentityErrors.InvalidCredentials, "Invalid credentials.");
        }

        var passwordValid = await _userManager.CheckPasswordAsync(user, request.CurrentPassword);
        if (!passwordValid)
        {
            return ServiceResult<Unit>.Failure(ResultType.Unauthorized, IdentityErrors.InvalidCredentials, "Invalid credentials.");
        }

        var disableResult = await _userManager.SetTwoFactorEnabledAsync(user, false);
        if (!disableResult.Succeeded)
        {
            return ServiceResult<Unit>.Failure(ResultType.BadRequest, IdentityErrors.MfaResetFailed, "Failed to reset MFA.");
        }

        var resetResult = await _userManager.ResetAuthenticatorKeyAsync(user);
        if (!resetResult.Succeeded)
        {
            return ServiceResult<Unit>.Failure(ResultType.BadRequest, IdentityErrors.MfaResetFailed, "Failed to reset MFA.");
        }

        var updateResult = await _userManager.UpdateAsync(user);
        if (updateResult.Succeeded)
        {
            // Only once the change is stored. RevokeAllAsync commits on its own, so ending
            // the sessions first signed every device out even when this update failed.
            await _sessions.RevokeRefreshTokenAsync(user, cancellationToken);

            _activityLogger.LogAudit(
                action: "Account.MfaReset",
                actorUserId: user.Id,
                targetUserId: user.Id,
                reason: "User reset their own MFA.");
            return new ServiceResult<Unit>(ResultType.Success, Unit.Value);
        }

        return ServiceResult<Unit>.Failure(ResultType.BadRequest, IdentityErrors.MfaResetFailed, "Failed to reset MFA.");
    }
}
