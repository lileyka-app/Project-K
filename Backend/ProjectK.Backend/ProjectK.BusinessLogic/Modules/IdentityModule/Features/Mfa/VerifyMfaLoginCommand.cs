using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using ProjectK.BusinessLogic.Modules.IdentityModule.Services;
using ProjectK.Common.Contracts.InfrastructureModule;
using ProjectK.Common.Dtos.IdentityModule;
using ProjectK.Common.Entities.IdentityModule;
using ProjectK.Common.Enums.Shared;
using ProjectK.Common.Records.Shared;
using ProjectK.Common.Rules.IdentityModule;

namespace ProjectK.BusinessLogic.Modules.IdentityModule.Features.Mfa;

/// <summary>
/// Finishes a sign-in that stopped for the second factor. <paramref name="MfaToken"/> is the
/// challenge the password step handed out for this account; the handler refuses without it.
/// </summary>
public sealed record VerifyMfaLoginCommand(string Email, string Code, bool RememberMe, string? MfaToken = null)
    : IRequest<ServiceResult<LoginUserDto>>;

public sealed class VerifyMfaLoginCommandHandler : IRequestHandler<VerifyMfaLoginCommand, ServiceResult<LoginUserDto>>
{
    private const string InvalidCredentialsCode = IdentityErrors.InvalidCredentials;
    private const string InvalidCredentialsMessage = "Invalid verification code or recovery code.";

    private readonly UserManager<AppUser> _userManager;
    private readonly ILoginResponseService _loginResponseService;
    private readonly ILogger<VerifyMfaLoginCommandHandler> _logger;
    private readonly IActivityLogger _activityLogger;
    private readonly IJwtService _jwtService;

    public VerifyMfaLoginCommandHandler(
        UserManager<AppUser> userManager,
        ILoginResponseService loginResponseService,
        ILogger<VerifyMfaLoginCommandHandler> logger,
        IActivityLogger activityLogger,
        IJwtService jwtService)
    {
        _userManager = userManager;
        _loginResponseService = loginResponseService;
        _logger = logger;
        _activityLogger = activityLogger;
        _jwtService = jwtService;
    }

    public async Task<ServiceResult<LoginUserDto>> Handle(VerifyMfaLoginCommand request, CancellationToken cancellationToken)
    {
        // Suspended between the password step and this one is still suspended.
        var user = await _userManager.FindByEmailAsync(request.Email);
        if (user == null || !user.CanSignIn())
        {
            _activityLogger.TrackFailedMfa(request.Email);
            return ServiceResult<LoginUserDto>.Failure(ResultType.Unauthorized, InvalidCredentialsCode, InvalidCredentialsMessage);
        }

        // The second factor is only ever the second one. The challenge is minted by the password
        // step for this account and lives a few minutes; without it, a code alone — read over a
        // shoulder, or a recovery code that leaked — was a complete sign-in.
        var challengedUserKey = string.IsNullOrWhiteSpace(request.MfaToken)
            ? null
            : _jwtService.ReadMfaChallenge(request.MfaToken);

        if (challengedUserKey is null || challengedUserKey.Value != user.Id)
        {
            _activityLogger.TrackFailedMfa(request.Email);
            return ServiceResult<LoginUserDto>.Failure(ResultType.Unauthorized, InvalidCredentialsCode, InvalidCredentialsMessage);
        }

        var verificationCode = request.Code.Replace(" ", string.Empty).Replace("-", string.Empty);
        var isTokenValid = await _userManager.VerifyTwoFactorTokenAsync(
            user,
            _userManager.Options.Tokens.AuthenticatorTokenProvider,
            verificationCode);

        if (!isTokenValid)
        {
            var recoveryCodeResult = await _userManager.RedeemTwoFactorRecoveryCodeAsync(user, request.Code.Trim());
            if (!recoveryCodeResult.Succeeded)
            {
                _activityLogger.TrackFailedMfa(request.Email);
                return ServiceResult<LoginUserDto>.Failure(ResultType.Unauthorized, InvalidCredentialsCode, InvalidCredentialsMessage);
            }

            _logger.LogInformation("Audit: User {UserId} successfully logged in using a recovery code.", user.Id);
        }

        var response = await _loginResponseService.CreateAsync(user, cancellationToken);

        // The code was right once on this device; the browser keeps that, so the next sign-ins
        // here skip the second factor until the trust runs out or the account's stamp changes.
        var trusted = response with { MfaTrust = _jwtService.GenerateMfaTrustToken(user.Id, user.SecurityStamp ?? string.Empty) };

        return new ServiceResult<LoginUserDto>(ResultType.Success, trusted);
    }
}
