using MediatR;
using Microsoft.AspNetCore.Identity;
using ProjectK.BusinessLogic.Modules.AuthModule.Services;
using ProjectK.Common.Entities.AuthModule;
using ProjectK.Common.Extensions;
using ProjectK.Common.Interfaces.Modules.AuthModule;
using ProjectK.Common.Interfaces.Modules.InfrastructureModule;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Records;

namespace ProjectK.BusinessLogic.Modules.UsersModule.Features.User.Suspend;

/// <summary>
/// The switch between deleting an account and leaving it alone. A person who left the kurin
/// under a cloud, a phone that went missing with the authenticator on it, a leader who is away
/// for a season: none of these is a reason to destroy the account and everything it is linked
/// to, and all of them are reasons it must not open a session tomorrow.
/// </summary>
public class SuspendUserCommandHandler : IRequestHandler<SuspendUserCommand, ServiceResult<bool>>
{
    private readonly UserManager<AppUser> _userManager;
    private readonly ICurrentUserContext _currentUserContext;
    private readonly RefreshTokenInvalidation _sessions;
    private readonly IActivityLogger _activityLogger;

    public SuspendUserCommandHandler(
        UserManager<AppUser> userManager,
        ICurrentUserContext currentUserContext,
        RefreshTokenInvalidation sessions,
        IActivityLogger activityLogger)
    {
        _userManager = userManager;
        _currentUserContext = currentUserContext;
        _sessions = sessions;
        _activityLogger = activityLogger;
    }

    public async Task<ServiceResult<bool>> Handle(SuspendUserCommand request, CancellationToken cancellationToken)
    {
        var target = await _userManager.FindByIdAsync(request.TargetUserKey.ToString());
        if (target is null)
        {
            return ServiceResult<bool>.Failure(ResultType.NotFound, "UserNotFound", "Target user not found.");
        }

        // The one account an administrator cannot suspend is their own: that would be the last
        // administrator locking the door from the outside.
        if (target.Id == _currentUserContext.UserId)
        {
            return ServiceResult<bool>.Failure(ResultType.BadRequest, "CannotSuspendSelf", "You cannot suspend your own account.");
        }

        if (!target.CanSignIn())
        {
            return new ServiceResult<bool>(ResultType.Success, true);
        }

        target.OnboardingStatus = OnboardingStatus.Suspended;
        var update = await _userManager.UpdateAsync(target);
        if (!update.Succeeded)
        {
            return ServiceResult<bool>.Failure(ResultType.BadRequest, "SuspendFailed", "Failed to suspend the account.");
        }

        // Only once the status is stored: RevokeAllAsync commits on its own, and ending the
        // sessions of an account that then stayed active would just have signed them out once.
        await _sessions.RevokeRefreshTokenAsync(target, CalendarFeedRevokeReason.Suspended, cancellationToken);

        _activityLogger.LogAudit(
            action: "Admin.UserSuspended",
            actorUserId: _currentUserContext.UserId,
            targetUserId: target.Id,
            reason: "Administrator suspended an account; all its sessions were ended.");

        return new ServiceResult<bool>(ResultType.Success, true);
    }
}

public class RestoreUserCommandHandler : IRequestHandler<RestoreUserCommand, ServiceResult<bool>>
{
    private readonly UserManager<AppUser> _userManager;
    private readonly ICurrentUserContext _currentUserContext;
    private readonly IActivityLogger _activityLogger;

    public RestoreUserCommandHandler(
        UserManager<AppUser> userManager,
        ICurrentUserContext currentUserContext,
        IActivityLogger activityLogger)
    {
        _userManager = userManager;
        _currentUserContext = currentUserContext;
        _activityLogger = activityLogger;
    }

    public async Task<ServiceResult<bool>> Handle(RestoreUserCommand request, CancellationToken cancellationToken)
    {
        var target = await _userManager.FindByIdAsync(request.TargetUserKey.ToString());
        if (target is null)
        {
            return ServiceResult<bool>.Failure(ResultType.NotFound, "UserNotFound", "Target user not found.");
        }

        if (target.CanSignIn())
        {
            return new ServiceResult<bool>(ResultType.Success, true);
        }

        target.OnboardingStatus = OnboardingStatus.Active;
        var update = await _userManager.UpdateAsync(target);
        if (!update.Succeeded)
        {
            return ServiceResult<bool>.Failure(ResultType.BadRequest, "RestoreFailed", "Failed to restore the account.");
        }

        _activityLogger.LogAudit(
            action: "Admin.UserRestored",
            actorUserId: _currentUserContext.UserId,
            targetUserId: target.Id,
            reason: "Administrator restored a suspended account.");

        return new ServiceResult<bool>(ResultType.Success, true);
    }
}
