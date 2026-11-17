using MediatR;
using Microsoft.AspNetCore.Identity;
using ProjectK.BusinessLogic.Modules.IdentityModule.Services;
using ProjectK.Common.Contracts.IdentityModule;
using ProjectK.Common.Contracts.NotificationsModule;
using ProjectK.Common.Entities.IdentityModule;
using ProjectK.Common.Enums.IdentityModule;
using ProjectK.Common.Enums.Shared;
using ProjectK.Common.Records.Shared;
using ProjectK.Common.Rules.IdentityModule;

namespace ProjectK.BusinessLogic.Modules.IdentityModule.Features.Users;

/// <summary>Refuses the account at sign-in and ends every session it holds. Reversible.</summary>
public sealed record SuspendUserCommand(Guid TargetUserKey) : IRequest<ServiceResult<Unit>>;

/// <summary>
/// The switch between deleting an account and leaving it alone. A person who left the kurin
/// under a cloud, a phone that went missing with the authenticator on it, a leader who is away
/// for a season: none of these is a reason to destroy the account and everything it is linked
/// to, and all of them are reasons it must not open a session tomorrow.
/// </summary>
public sealed class SuspendUserCommandHandler : IRequestHandler<SuspendUserCommand, ServiceResult<Unit>>
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

    public async Task<ServiceResult<Unit>> Handle(SuspendUserCommand request, CancellationToken cancellationToken)
    {
        var target = await _userManager.FindByIdAsync(request.TargetUserKey.ToString());
        if (target is null)
        {
            return IdentityErrors.TargetNotFound<Unit>();
        }

        // The one account an administrator cannot suspend is their own: that would be the last
        // administrator locking the door from the outside.
        if (target.Id == _currentUserContext.UserId)
        {
            return ServiceResult<Unit>.Failure(ResultType.BadRequest, IdentityErrors.CannotSuspendSelf, "You cannot suspend your own account.");
        }

        if (!target.CanSignIn())
        {
            return new ServiceResult<Unit>(ResultType.Success, Unit.Value);
        }

        target.OnboardingStatus = OnboardingStatus.Suspended;
        var update = await _userManager.UpdateAsync(target);
        if (!update.Succeeded)
        {
            return ServiceResult<Unit>.Failure(ResultType.BadRequest, IdentityErrors.SuspendFailed, "Failed to suspend the account.");
        }

        // Only once the status is stored: RevokeAllAsync commits on its own, and ending the
        // sessions of an account that then stayed active would just have signed them out once.
        await _sessions.RevokeRefreshTokenAsync(target, CalendarFeedRevokeReason.Suspended, cancellationToken);

        _activityLogger.LogAudit(
            action: "Admin.UserSuspended",
            actorUserId: _currentUserContext.UserId,
            targetUserId: target.Id,
            reason: "Administrator suspended an account; all its sessions were ended.");

        return new ServiceResult<Unit>(ResultType.Success, Unit.Value);
    }
}
