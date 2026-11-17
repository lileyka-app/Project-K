using MediatR;
using Microsoft.AspNetCore.Identity;
using ProjectK.Common.Contracts.IdentityModule;
using ProjectK.Common.Contracts.InfrastructureModule;
using ProjectK.Common.Entities.IdentityModule;
using ProjectK.Common.Enums.Shared;
using ProjectK.Common.Records.Shared;

using ProjectK.Common.Rules.IdentityModule;

namespace ProjectK.BusinessLogic.Modules.IdentityModule.Features.Account;

public sealed record ChangeOwnPasswordCommand(Guid UserKey, string CurrentPassword, string NewPassword)
    : IRequest<ServiceResult<Unit>>;

public sealed class ChangeOwnPasswordCommandHandler : IRequestHandler<ChangeOwnPasswordCommand, ServiceResult<Unit>>
{
    private readonly UserManager<AppUser> _userManager;
    private readonly IAccountSessionWriter _sessions;
    private readonly IActivityLogger _activityLogger;

    public ChangeOwnPasswordCommandHandler(
        UserManager<AppUser> userManager,
        IActivityLogger activityLogger,
        IAccountSessionWriter sessions)
    {
        _userManager = userManager;
        _activityLogger = activityLogger;
        _sessions = sessions;
    }

    public async Task<ServiceResult<Unit>> Handle(ChangeOwnPasswordCommand request, CancellationToken cancellationToken)
    {
        var user = await _userManager.FindSignedInAsync(request.UserKey);
        if (user == null)
        {
            return IdentityErrors.AccountMissing<Unit>();
        }

        var result = await _userManager.ChangePasswordAsync(user, request.CurrentPassword, request.NewPassword);
        if (!result.Succeeded)
        {
            return ServiceResult<Unit>.Failure(ResultType.BadRequest, IdentityErrors.PasswordChangeFailed, "Failed to change password. Please check your current password and try again.");
        }

        var updateResult = await _userManager.UpdateAsync(user);
        if (updateResult.Succeeded)
        {
            // Only once the change is stored. RevokeAllAsync commits on its own, so ending
            // the sessions first signed every device out even when this update failed.
            await _sessions.RevokeRefreshTokenAsync(user, cancellationToken);

            _activityLogger.LogAudit(
                action: "Account.PasswordChanged",
                actorUserId: user.Id,
                targetUserId: user.Id,
                reason: "User changed their own password.");
            return new ServiceResult<Unit>(ResultType.Success, Unit.Value);
        }

        return ServiceResult<Unit>.Failure(ResultType.BadRequest, IdentityErrors.PasswordChangeFailed, "Failed to change password. Please try again.");
    }
}
