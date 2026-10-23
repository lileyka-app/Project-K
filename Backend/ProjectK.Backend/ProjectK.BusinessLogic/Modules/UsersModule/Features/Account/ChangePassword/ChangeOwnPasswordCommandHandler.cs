using MediatR;
using Microsoft.AspNetCore.Identity;
using ProjectK.BusinessLogic.Modules.AuthModule.Services;
using ProjectK.Common.Entities.AuthModule;
using ProjectK.Common.Interfaces.Modules.AuthModule;
using ProjectK.Common.Interfaces.Modules.InfrastructureModule;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Records;

namespace ProjectK.BusinessLogic.Modules.UsersModule.Features.Account.ChangePassword;

public class ChangeOwnPasswordCommandHandler : IRequestHandler<ChangeOwnPasswordCommand, ServiceResult<bool>>
{
    private readonly UserManager<AppUser> _userManager;
    private readonly RefreshTokenInvalidation _sessions;
    private readonly IActivityLogger _activityLogger;

    public ChangeOwnPasswordCommandHandler(
        UserManager<AppUser> userManager,
        IActivityLogger activityLogger,
        RefreshTokenInvalidation sessions)
    {
        _userManager = userManager;
        _activityLogger = activityLogger;
        _sessions = sessions;
    }

    public async Task<ServiceResult<bool>> Handle(ChangeOwnPasswordCommand request, CancellationToken cancellationToken)
    {
        var user = await _userManager.FindByIdAsync(request.UserKey.ToString());
        if (user == null)
        {
            return ServiceResult<bool>.Failure(ResultType.Unauthorized, "Unauthorized", "User not found or unauthorized.");
        }

        var result = await _userManager.ChangePasswordAsync(user, request.CurrentPassword, request.NewPassword);
        if (!result.Succeeded)
        {
            return ServiceResult<bool>.Failure(ResultType.BadRequest, "PasswordChangeFailed", "Failed to change password. Please check your current password and try again.");
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
            return new ServiceResult<bool>(ResultType.Success, true);
        }

        return ServiceResult<bool>.Failure(ResultType.BadRequest, "PasswordChangeFailed", "Failed to change password. Please try again.");
    }
}
