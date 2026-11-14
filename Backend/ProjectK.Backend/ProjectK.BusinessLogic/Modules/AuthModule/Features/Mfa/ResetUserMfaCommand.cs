using MediatR;
using Microsoft.AspNetCore.Identity;
using ProjectK.BusinessLogic.Modules.AuthModule.Services;
using ProjectK.Common.Contracts.AuthModule;
using ProjectK.Common.Contracts.InfrastructureModule;
using ProjectK.Common.Contracts.KurinModule;
using ProjectK.Common.Entities.AuthModule;
using ProjectK.Common.Enums.Shared;
using ProjectK.Common.Records.Shared;
using ProjectK.Common.Rules.AuthModule;

namespace ProjectK.BusinessLogic.Modules.AuthModule.Features.Mfa;

public record ResetUserMfaCommand(Guid TargetUserKey) : IRequest<ServiceResult<Unit>>;

public class ResetUserMfaCommandHandler : IRequestHandler<ResetUserMfaCommand, ServiceResult<Unit>>
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

    public async Task<ServiceResult<Unit>> Handle(ResetUserMfaCommand request, CancellationToken cancellationToken)
    {
        var targetUser = await _userManager.FindByIdAsync(request.TargetUserKey.ToString());
        if (targetUser == null)
        {
            return ServiceResult<Unit>.Failure(ResultType.NotFound, AuthErrors.UserNotFound, "Target user not found.");
        }

        var isAdmin = _currentUserContext.IsAdmin();
        var isKurinManager = !isAdmin && _currentUserContext.CanManageWholeKurin();
        if (!isAdmin && !isKurinManager)
        {
            return ServiceResult<Unit>.Failure(ResultType.Forbidden, AuthErrors.Forbidden, "You do not have permission to perform this action.");
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
                return ServiceResult<Unit>.Failure(ResultType.Forbidden, AuthErrors.Forbidden, "Kurin managers can reset MFA only in their own Kurin.");
            }

            if (RolePermissionRules.GrantsWholeKurinManagement(targetRoles))
            {
                return ServiceResult<Unit>.Failure(ResultType.Forbidden, AuthErrors.CannotResetPrivilegedMfa, "Kurin managers cannot reset MFA for privileged users.");
            }
        }

        if (targetUser.TwoFactorEnabled)
        {
            var disableResult = await _userManager.SetTwoFactorEnabledAsync(targetUser, false);
            if (!disableResult.Succeeded)
            {
                return ServiceResult<Unit>.Failure(ResultType.BadRequest, AuthErrors.MfaResetFailed, "Failed to reset MFA.");
            }
        }

        var resetResult = await _userManager.ResetAuthenticatorKeyAsync(targetUser);
        if (!resetResult.Succeeded)
        {
            return ServiceResult<Unit>.Failure(ResultType.BadRequest, AuthErrors.MfaResetFailed, "Failed to reset MFA.");
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
            return new ServiceResult<Unit>(ResultType.Success, Unit.Value);
        }

        return ServiceResult<Unit>.Failure(ResultType.BadRequest, AuthErrors.MfaResetFailed, "Failed to reset MFA.");
    }
}
