using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using ProjectK.BusinessLogic.Modules.AuthModule.Models;
using ProjectK.BusinessLogic.Modules.AuthModule.Services;
using ProjectK.Common.Entities.AuthModule;
using ProjectK.Common.Extensions;
using ProjectK.Common.Interfaces.Modules.InfrastructureModule;
using ProjectK.Common.Models.Dtos.AuthModule;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Records;

namespace ProjectK.BusinessLogic.Modules.AuthModule.Features.Mfa;

/// <summary>
/// Finishes a sign-in that stopped for the second factor. <paramref name="MfaToken"/> is the
/// challenge the password step handed out for this account; the handler refuses without it.
/// </summary>
public record VerifyMfaLoginCommand(string Email, string Code, bool RememberMe, string? MfaToken = null)
    : IRequest<ServiceResult<LoginUserDto>>;

public class VerifyMfaLoginCommandHandler : IRequestHandler<VerifyMfaLoginCommand, ServiceResult<LoginUserDto>>
{
    private const string InvalidCredentialsCode = AuthErrors.InvalidCredentials;
    private const string InvalidCredentialsMessage = "Invalid verification code or recovery code.";

    private readonly UserManager<AppUser> _userManager;
    private readonly ILoginResponseFactory _loginResponseFactory;
    private readonly ILogger<VerifyMfaLoginCommandHandler> _logger;
    private readonly IActivityLogger _activityLogger;
    private readonly IJwtService _jwtService;

    public VerifyMfaLoginCommandHandler(
        UserManager<AppUser> userManager,
        ILoginResponseFactory loginResponseFactory,
        ILogger<VerifyMfaLoginCommandHandler> logger,
        IActivityLogger activityLogger,
        IJwtService jwtService)
    {
        _userManager = userManager;
        _loginResponseFactory = loginResponseFactory;
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

        var response = await _loginResponseFactory.CreateAsync(user, cancellationToken);

        // The code was right once on this device; the browser keeps that, so the next sign-ins
        // here skip the second factor until the trust runs out or the account's stamp changes.
        var trusted = response with { MfaTrust = _jwtService.GenerateMfaTrustToken(user.Id, user.SecurityStamp ?? string.Empty) };

        return new ServiceResult<LoginUserDto>(ResultType.Success, trusted);
    }
}
