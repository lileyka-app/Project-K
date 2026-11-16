using MediatR;
using Microsoft.AspNetCore.Identity;
using ProjectK.BusinessLogic.Modules.AuthModule.Services;
using ProjectK.Common.Contracts.AuthModule;
using ProjectK.Common.Contracts.InfrastructureModule;
using ProjectK.Common.Entities.AuthModule;
using ProjectK.Common.Enums.Shared;
using ProjectK.Common.Records.Shared;
using ProjectK.Common.Rules.AuthModule;

namespace ProjectK.BusinessLogic.Modules.AuthModule.Features.Mfa;

public sealed record DisableOwnMfaCommand(Guid UserKey, string CurrentPassword) : IRequest<ServiceResult<Unit>>;

public sealed class DisableOwnMfaCommandHandler : IRequestHandler<DisableOwnMfaCommand, ServiceResult<Unit>>
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

    public async Task<ServiceResult<Unit>> Handle(DisableOwnMfaCommand request, CancellationToken cancellationToken)
    {
        var user = await _userManager.FindSignedInAsync(request.UserKey);
        if (user == null)
        {
            return AuthErrors.AccountMissing<Unit>();
        }

        if (string.IsNullOrWhiteSpace(request.CurrentPassword)
            || !await _userManager.CheckPasswordAsync(user, request.CurrentPassword))
        {
            return ServiceResult<Unit>.Failure(ResultType.Unauthorized, AuthErrors.InvalidCredentials, "Invalid credentials.");
        }

        var roles = (await _access.ResolveAsync(user, cancellationToken)).Roles;
        if (RolePermissionRules.GrantsWholeKurinManagement(roles))
        {
            return ServiceResult<Unit>.Failure(ResultType.Forbidden, AuthErrors.MfaRequired, "Privileged users must keep MFA enabled. Reset MFA to reconfigure it.");
        }

        if (!user.TwoFactorEnabled)
        {
            return new ServiceResult<Unit>(ResultType.Success, Unit.Value);
        }

        var disableResult = await _userManager.SetTwoFactorEnabledAsync(user, false);
        if (!disableResult.Succeeded)
        {
            return ServiceResult<Unit>.Failure(ResultType.BadRequest, AuthErrors.MfaDisableFailed, "Failed to disable MFA.");
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
            return new ServiceResult<Unit>(ResultType.Success, Unit.Value);
        }

        return ServiceResult<Unit>.Failure(ResultType.BadRequest, AuthErrors.MfaDisableFailed, "Failed to disable MFA.");
    }
}
