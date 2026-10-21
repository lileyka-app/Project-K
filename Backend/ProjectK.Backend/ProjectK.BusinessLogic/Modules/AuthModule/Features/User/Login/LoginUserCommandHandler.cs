using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using ProjectK.BusinessLogic.Modules.AuthModule.Models;
using ProjectK.BusinessLogic.Modules.AuthModule.Services;
using ProjectK.Common.Entities.AuthModule;
using ProjectK.Common.Extensions;
using ProjectK.Common.Interfaces.Modules.InfrastructureModule;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Records;

namespace ProjectK.BusinessLogic.Modules.AuthModule.Features.User.Login;

public class LoginUserCommandHandler : IRequestHandler<LoginUserCommand, ServiceResult<LoginUserResponse>>
{
    private readonly UserManager<AppUser> _userManager;
    private readonly SignInManager<AppUser> _signInManager;
    private readonly ILoginResponseFactory _loginResponseFactory;
    private readonly IActivityLogger _activityLogger;
    private readonly IConfiguration _configuration;
    private readonly IJwtService _jwtService;
    private readonly IHostEnvironment _environment;
    private readonly ILoginThrottle _throttle;

    public LoginUserCommandHandler(
        UserManager<AppUser> userManager,
        SignInManager<AppUser> signInManager,
        ILoginResponseFactory loginResponseFactory,
        IActivityLogger activityLogger,
        IConfiguration configuration,
        IJwtService jwtService,
        IHostEnvironment environment,
        ILoginThrottle throttle)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _loginResponseFactory = loginResponseFactory;
        _activityLogger = activityLogger;
        _configuration = configuration;
        _jwtService = jwtService;
        _environment = environment;
        _throttle = throttle;
    }

    public async Task<ServiceResult<LoginUserResponse>> Handle(LoginUserCommand request, CancellationToken cancellationToken)
    {
        // Checked before the address is looked up, and counted for unknown addresses too: a pause
        // answers the same whether or not anyone is registered under it.
        if (_throttle.IsPaused(request.Email))
        {
            return ServiceResult<LoginUserResponse>.Failure(
                ResultType.TooManyRequests,
                "TooManyAttempts",
                "Too many failed sign-ins for this email. Try again later or reset the password.");
        }

        // Both failure branches answer identically on purpose: a distinct "no such user" reply
        // would let a caller enumerate which addresses are registered.
        var user = await _userManager.FindByEmailAsync(request.Email);
        if (user == null)
        {
            _activityLogger.TrackFailedLogin(request.Email);
            _throttle.RecordFailure(request.Email);
            return ServiceResult<LoginUserResponse>.Failure(
                ResultType.Unauthorized,
                "InvalidCredentials",
                "Email or password is incorrect.");
        }

        var result = await _signInManager.CheckPasswordSignInAsync(user, request.Password, false);

        // A suspended account answers exactly like a wrong password: the person who was
        // suspended already knows why, and nobody else is owed the difference.
        if (!result.Succeeded || !user.CanSignIn())
        {
            _activityLogger.TrackFailedLogin(request.Email);
            _throttle.RecordFailure(request.Email);
            return ServiceResult<LoginUserResponse>.Failure(
                ResultType.Unauthorized,
                "InvalidCredentials",
                "Email or password is incorrect.");
        }

        _throttle.Reset(request.Email);

        // The e2e switch lets the fixture accounts sign in without a second factor, and only on
        // the test tiers: a deployed environment ignores it outright, exactly as
        // MfaEnforcementPolicy does for the enforcement gate. Read without that guard, one config
        // key turned every account's second factor off in production.
        var bypassMfa = !_environment.IsProduction()
            && !_environment.IsStaging()
            && _configuration.GetValue<bool>("E2E:BypassPrivilegedMfa");

        if (user.TwoFactorEnabled && !bypassMfa && !IsTrustedDevice(request.MfaTrustToken, user))
        {
            return new ServiceResult<LoginUserResponse>(
                ResultType.Success,
                new LoginUserResponse
                {
                    UserKey = user.Id,
                    Email = user.Email!,
                    RequiresMfa = true,
                    // What the second step has to bring back: proof that this password step happened.
                    MfaToken = _jwtService.GenerateMfaChallengeToken(user.Id)
                });
        }

        var response = await _loginResponseFactory.CreateAsync(user, cancellationToken);

        return new ServiceResult<LoginUserResponse>(
            ResultType.Success,
            response);
    }

    /// <summary>
    /// A device that finished the second factor recently is not asked again. The token is tied to
    /// the security stamp the account had then: a password change or an MFA reset rotates the
    /// stamp, and every device has to prove itself once more.
    /// </summary>
    private bool IsTrustedDevice(string? trustToken, AppUser user)
    {
        if (string.IsNullOrWhiteSpace(trustToken))
        {
            return false;
        }

        var trust = _jwtService.ReadMfaTrust(trustToken);
        return trust is not null
            && trust.UserId == user.Id
            && string.Equals(trust.SecurityStamp, user.SecurityStamp, StringComparison.Ordinal);
    }
}
