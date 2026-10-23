using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.AspNetCore.Identity;
using ProjectK.BusinessLogic.Modules.AuthModule.Services;
using ProjectK.Common.Entities.AuthModule;
using ProjectK.Common.Interfaces.Modules.InfrastructureModule;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Records;

namespace ProjectK.BusinessLogic.Modules.AuthModule.Features.Onboarding.ResetPassword;

public class ResetPasswordCommandHandler : IRequestHandler<ResetPasswordCommand, ServiceResult<bool>>
{
    private readonly UserManager<AppUser> _userManager;
    private readonly IActivityLogger _activityLogger;
    private readonly RefreshTokenInvalidation _sessions;

    public ResetPasswordCommandHandler(
        UserManager<AppUser> userManager,
        IActivityLogger activityLogger,
        RefreshTokenInvalidation sessions)
    {
        _userManager = userManager;
        _activityLogger = activityLogger;
        _sessions = sessions;
    }

    public async Task<ServiceResult<bool>> Handle(ResetPasswordCommand request, CancellationToken cancellationToken)
    {
        var user = await _userManager.FindByEmailAsync(request.Email);
        if (user == null || user.OnboardingStatus != OnboardingStatus.Active)
        {
            return ServiceResult<bool>.Failure(ResultType.BadRequest, "InvalidResetAttempt", "Invalid reset attempt. Please check your email or token.");
        }

        var result = await _userManager.ResetPasswordAsync(user, request.Token, request.NewPassword);
        if (!result.Succeeded)
        {
            return ServiceResult<bool>.Failure(ResultType.BadRequest, "InvalidResetAttempt", "Invalid reset attempt. Please check your email or token.");
        }

        // Sessions are not ended here yet (a gap of its own, tracked in planning); the calendar link
        // is, as on every other password change.
        await _sessions.RevokeCalendarFeedAsync(user.Id, CalendarFeedRevokeReason.SecurityChange, cancellationToken);

        _activityLogger.LogAudit(
            action: "Account.PasswordResetCompleted",
            actorUserId: user.Id,
            targetUserId: user.Id,
            email: user.Email,
            reason: "Password reset completed with reset token.");

        return new ServiceResult<bool>(ResultType.Success, true);
    }
}
