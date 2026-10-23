using MediatR;
using Microsoft.AspNetCore.Identity;
using ProjectK.BusinessLogic.Modules.AuthModule.Services;
using ProjectK.Common.Entities.AuthModule;
using ProjectK.Common.Interfaces.Modules.AuthModule;
using ProjectK.Common.Interfaces.Modules.InfrastructureModule;
using ProjectK.Common.Models.Authorization;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Records;

namespace ProjectK.BusinessLogic.Modules.UsersModule.Features.Account.DisableMfa;

public class DisableOwnMfaCommandHandler : IRequestHandler<DisableOwnMfaCommand, ServiceResult<bool>>
{
    private readonly UserManager<AppUser> _userManager;
    private readonly RefreshTokenInvalidation _sessions;
    private readonly IActivityLogger _activityLogger;
    private readonly IAccessContextResolver _access;

    public DisableOwnMfaCommandHandler(
        UserManager<AppUser> userManager,
        IActivityLogger activityLogger,
        RefreshTokenInvalidation sessions,
        IAccessContextResolver access)
    {
        _userManager = userManager;
        _activityLogger = activityLogger;
        _sessions = sessions;
        _access = access;
    }

    public async Task<ServiceResult<bool>> Handle(DisableOwnMfaCommand request, CancellationToken cancellationToken)
    {
        var user = await _userManager.FindByIdAsync(request.UserKey.ToString());
        if (user == null)
        {
            return ServiceResult<bool>.Failure(ResultType.Unauthorized, "Unauthorized", "User not found or unauthorized.");
        }

        if (string.IsNullOrWhiteSpace(request.CurrentPassword)
            || !await _userManager.CheckPasswordAsync(user, request.CurrentPassword))
        {
            return ServiceResult<bool>.Failure(ResultType.Unauthorized, "InvalidCredentials", "Invalid credentials.");
        }

        var roles = (await _access.ResolveAsync(user, cancellationToken)).Roles;
        if (RolePermissionMap.GrantsWholeKurinManagement(roles))
        {
            return ServiceResult<bool>.Failure(ResultType.Forbidden, "MfaRequired", "Privileged users must keep MFA enabled. Reset MFA to reconfigure it.");
        }

        if (!user.TwoFactorEnabled)
        {
            return new ServiceResult<bool>(ResultType.Success, true);
        }

        var disableResult = await _userManager.SetTwoFactorEnabledAsync(user, false);
        if (!disableResult.Succeeded)
        {
            return ServiceResult<bool>.Failure(ResultType.BadRequest, "MfaDisableFailed", "Failed to disable MFA.");
        }

        var updateResult = await _userManager.UpdateAsync(user);
        if (updateResult.Succeeded)
        {
            // Only once the change is stored. RevokeAllAsync commits on its own, so ending
            // the sessions first signed every device out even when this update failed.
            await _sessions.RevokeRefreshTokenAsync(user, cancellationToken);

            _activityLogger.LogAudit(
                action: "Account.MfaDisabled",
                actorUserId: user.Id,
                targetUserId: user.Id,
                reason: "User disabled their own MFA.");
            return new ServiceResult<bool>(ResultType.Success, true);
        }

        return ServiceResult<bool>.Failure(ResultType.BadRequest, "MfaDisableFailed", "Failed to disable MFA.");
    }
}
