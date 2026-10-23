using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using ProjectK.BusinessLogic.Modules.AuthModule.Services;
using ProjectK.Common.Entities.AuthModule;
using ProjectK.Common.Extensions;
using ProjectK.Common.Interfaces.Modules.AuthModule;
using ProjectK.Common.Interfaces.Modules.InfrastructureModule;
using ProjectK.Common.Interfaces.Modules.KurinModule;
using ProjectK.Common.Models.Authorization;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Records;

namespace ProjectK.BusinessLogic.Modules.UsersModule.Features.User.ResetMfa;

public class ResetUserMfaCommandHandler : IRequestHandler<ResetUserMfaCommand, ServiceResult<bool>>
{
    private readonly UserManager<AppUser> _userManager;
    private readonly RefreshTokenInvalidation _sessions;
    private readonly ICurrentUserContext _currentUserContext;
    private readonly IActivityLogger _activityLogger;
    private readonly IAccessContextResolver _access;
    private readonly IMembershipDirectory _memberships;

    public ResetUserMfaCommandHandler(
        UserManager<AppUser> userManager,
        ICurrentUserContext currentUserContext,
        IActivityLogger activityLogger,
        RefreshTokenInvalidation sessions,
        IAccessContextResolver access,
        IMembershipDirectory memberships)
    {
        _userManager = userManager;
        _currentUserContext = currentUserContext;
        _activityLogger = activityLogger;
        _sessions = sessions;
        _access = access;
        _memberships = memberships;
    }

    public async Task<ServiceResult<bool>> Handle(ResetUserMfaCommand request, CancellationToken cancellationToken)
    {
        var targetUser = await _userManager.FindByIdAsync(request.TargetUserKey.ToString());
        if (targetUser == null)
        {
            return ServiceResult<bool>.Failure(ResultType.NotFound, "UserNotFound", "Target user not found.");
        }

        var isAdmin = _currentUserContext.IsAdmin();
        var isKurinManager = !isAdmin && _currentUserContext.CanManageWholeKurin();
        if (!isAdmin && !isKurinManager)
        {
            return ServiceResult<bool>.Failure(ResultType.Forbidden, "Forbidden", "You do not have permission to perform this action.");
        }

        var targetRoles = (await _access.ResolveAsync(targetUser, cancellationToken)).Roles;
        if (isKurinManager)
        {
            // Asked of membership, never of AppUser.KurinKey: that field is a snapshot taken
            // when the account was opened and nothing updates it afterwards, so someone who has
            // since moved on still carries their old kurin — and resetting MFA is the one lever
            // that hands an account to whoever pulls it.
            var managerKurinKey = _currentUserContext.KurinKey;
            var targetKurinKeys = await _memberships.GetKurinKeysForAccountAsync(targetUser.Id, cancellationToken);
            if (!managerKurinKey.HasValue || !targetKurinKeys.Contains(managerKurinKey.Value))
            {
                return ServiceResult<bool>.Failure(ResultType.Forbidden, "Forbidden", "Kurin managers can reset MFA only in their own Kurin.");
            }

            if (RolePermissionMap.GrantsWholeKurinManagement(targetRoles))
            {
                return ServiceResult<bool>.Failure(ResultType.Forbidden, "CannotResetPrivilegedMfa", "Kurin managers cannot reset MFA for privileged users.");
            }
        }

        if (targetUser.TwoFactorEnabled)
        {
            var disableResult = await _userManager.SetTwoFactorEnabledAsync(targetUser, false);
            if (!disableResult.Succeeded)
            {
                return ServiceResult<bool>.Failure(ResultType.BadRequest, "MfaResetFailed", "Failed to reset MFA.");
            }
        }

        var resetResult = await _userManager.ResetAuthenticatorKeyAsync(targetUser);
        if (!resetResult.Succeeded)
        {
            return ServiceResult<bool>.Failure(ResultType.BadRequest, "MfaResetFailed", "Failed to reset MFA.");
        }

        var updateResult = await _userManager.UpdateAsync(targetUser);
        if (updateResult.Succeeded)
        {
            // Only once the change is stored. RevokeAllAsync commits on its own, so ending
            // the sessions first signed every device out even when this update failed.
            await _sessions.RevokeRefreshTokenAsync(targetUser, cancellationToken);

            _activityLogger.LogAudit(
                action: "Admin.UserMfaReset",
                actorUserId: _currentUserContext.UserId,
                targetUserId: targetUser.Id,
                reason: "Privileged user reset MFA for another account.");
            return new ServiceResult<bool>(ResultType.Success, true);
        }

        return ServiceResult<bool>.Failure(ResultType.BadRequest, "MfaResetFailed", "Failed to reset MFA.");
    }
}
