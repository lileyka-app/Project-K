using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Identity;
using ProjectK.BusinessLogic.Modules.IdentityModule.Services;
using ProjectK.Common.Contracts.InfrastructureModule;
using ProjectK.Common.Entities.IdentityModule;
using ProjectK.Common.Enums.IdentityModule;
using ProjectK.Common.Enums.Shared;
using ProjectK.Common.Records.Shared;

namespace ProjectK.BusinessLogic.Modules.IdentityModule.Features.Onboarding;

public sealed record ResetPasswordCommand(string Email, string Token, string NewPassword) : IRequest<ServiceResult<Unit>>;

public sealed class ResetPasswordCommandValidator : AbstractValidator<ResetPasswordCommand>
{
    public ResetPasswordCommandValidator()
    {
        RuleFor(command => command.Email).NotEmpty().EmailAddress();
        RuleFor(command => command.Token).NotEmpty();
        // Presence only; password complexity stays with the Identity password policy.
        RuleFor(command => command.NewPassword).NotEmpty();
    }
}

public sealed class ResetPasswordCommandHandler : IRequestHandler<ResetPasswordCommand, ServiceResult<Unit>>
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

    public async Task<ServiceResult<Unit>> Handle(ResetPasswordCommand request, CancellationToken cancellationToken)
    {
        var user = await _userManager.FindByEmailAsync(request.Email);
        if (user == null || user.OnboardingStatus != OnboardingStatus.Active)
        {
            return ServiceResult<Unit>.Failure(ResultType.BadRequest, IdentityErrors.InvalidResetAttempt, "Invalid reset attempt. Please check your email or token.");
        }

        var result = await _userManager.ResetPasswordAsync(user, request.Token, request.NewPassword);
        if (!result.Succeeded)
        {
            return ServiceResult<Unit>.Failure(ResultType.BadRequest, IdentityErrors.InvalidResetAttempt, "Invalid reset attempt. Please check your email or token.");
        }

        // A reset is what someone does when they suspect the password is known: a session opened
        // with the old one must not outlive it, here as on every other password change.
        await _sessions.RevokeRefreshTokenAsync(user, cancellationToken);

        _activityLogger.LogAudit(
            action: "Account.PasswordResetCompleted",
            actorUserId: user.Id,
            targetUserId: user.Id,
            email: user.Email,
            reason: "Password reset completed with reset token.");

        return new ServiceResult<Unit>(ResultType.Success, Unit.Value);
    }
}
