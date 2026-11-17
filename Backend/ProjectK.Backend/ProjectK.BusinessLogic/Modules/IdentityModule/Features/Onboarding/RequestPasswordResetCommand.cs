using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using ProjectK.Common.Contracts.InfrastructureModule;
using ProjectK.Common.Entities.IdentityModule;
using ProjectK.Common.Enums.IdentityModule;
using ProjectK.Common.Enums.Shared;
using ProjectK.Common.Records.Shared;

namespace ProjectK.BusinessLogic.Modules.IdentityModule.Features.Onboarding;

public sealed record RequestPasswordResetCommand(string Email) : IRequest<ServiceResult<Unit>>;

/// <summary>
/// Example validator wired through <c>ValidationBehavior</c>: a malformed or empty email is
/// rejected with BadRequest before the handler runs. Well-formed unknown emails still pass
/// (the handler keeps its anti-enumeration behaviour).
/// </summary>
public sealed class RequestPasswordResetCommandValidator : AbstractValidator<RequestPasswordResetCommand>
{
    public RequestPasswordResetCommandValidator()
    {
        RuleFor(command => command.Email)
            .NotEmpty()
            .EmailAddress();
    }
}

public sealed class RequestPasswordResetCommandHandler : IRequestHandler<RequestPasswordResetCommand, ServiceResult<Unit>>
{
    private readonly UserManager<AppUser> _userManager;
    private readonly IEmailService _emailService;
    private readonly IActivityLogger _activityLogger;
    private readonly ILogger<RequestPasswordResetCommandHandler> _logger;

    public RequestPasswordResetCommandHandler(
        UserManager<AppUser> userManager,
        IEmailService emailService,
        IActivityLogger activityLogger,
        ILogger<RequestPasswordResetCommandHandler> logger)
    {
        _userManager = userManager;
        _emailService = emailService;
        _activityLogger = activityLogger;
        _logger = logger;
    }

    public async Task<ServiceResult<Unit>> Handle(RequestPasswordResetCommand request, CancellationToken cancellationToken)
    {
        var user = await _userManager.FindByEmailAsync(request.Email);

        // For security reasons, we return success even if user not found, 
        // but we don't send the email.
        if (user != null && user.OnboardingStatus == OnboardingStatus.Active)
        {
            var token = await _userManager.GeneratePasswordResetTokenAsync(user);
            _activityLogger.LogAudit(
                action: "Account.PasswordResetRequested",
                actorUserId: user.Id,
                targetUserId: user.Id,
                email: user.Email,
                reason: "Password reset email requested.");
            // The promise above only holds if a failure looks the same as an unknown address. A
            // send that throws would answer 500 for addresses we know and 200 for the rest, which
            // tells apart exactly what this endpoint refuses to tell apart.
            try
            {
                await _emailService.SendPasswordResetEmailAsync(user.Email!, token, cancellationToken);
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Could not send a password reset email.");
            }
        }

        return new ServiceResult<Unit>(ResultType.Success, Unit.Value);
    }
}
