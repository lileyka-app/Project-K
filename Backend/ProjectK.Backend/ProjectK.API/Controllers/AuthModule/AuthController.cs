using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text.Json.Serialization;
using AutoMapper;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using ProjectK.API.Authorization;
using ProjectK.API.Extensions;
using ProjectK.API.Helpers;
using ProjectK.BusinessLogic.Modules.AuthModule.Features.Access.Check;
using ProjectK.BusinessLogic.Modules.AuthModule.Features.KurinScope.Options;
using ProjectK.BusinessLogic.Modules.AuthModule.Features.LoadTest;
using ProjectK.BusinessLogic.Modules.AuthModule.Features.KurinScope.Set;
using ProjectK.BusinessLogic.Modules.AuthModule.Features.RefreshToken.Refresh;
using ProjectK.BusinessLogic.Modules.AuthModule.Features.SavedAccount.Add;
using ProjectK.BusinessLogic.Modules.AuthModule.Features.SavedAccount.Get;
using ProjectK.BusinessLogic.Modules.AuthModule.Features.SavedAccount.Switch;
using ProjectK.BusinessLogic.Modules.AuthModule.Features.User.EnableMfa;
using ProjectK.BusinessLogic.Modules.AuthModule.Features.User.GenerateMfaRecoveryCodes;
using ProjectK.BusinessLogic.Modules.AuthModule.Features.User.GetMfaSetup;
using ProjectK.BusinessLogic.Modules.AuthModule.Features.User.GetMfaStatus;
using ProjectK.BusinessLogic.Modules.AuthModule.Features.User.Login;
using ProjectK.BusinessLogic.Modules.AuthModule.Features.User.Logout;
using ProjectK.BusinessLogic.Modules.AuthModule.Features.User.Register;
using ProjectK.BusinessLogic.Modules.AuthModule.Features.User.VerifyMfaLogin;
using ProjectK.BusinessLogic.Modules.AuthModule.Models;
using ProjectK.BusinessLogic.Modules.AuthModule.Services;
using ProjectK.BusinessLogic.Modules.UsersModule.Features.User.Get;
using ProjectK.BusinessLogic.Modules.UsersModule.Features.User.RegisterKurin;
using ProjectK.BusinessLogic.Modules.UsersModule.Models;
using ProjectK.Common.Extensions;
using ProjectK.Common.Models.Authorization;
using ProjectK.Common.Models.Dtos.AuthModule;
using ProjectK.Common.Models.Dtos.AuthModule.Requests;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Records;

namespace ProjectK.API.Controllers.AuthModule;

/// <summary>
/// Signing in, signing out, and the second factor. Account creation for people who are already known to
/// the kurin lives here; strangers apply through the waitlist on <c>api/auth/onboarding</c> instead.
/// </summary>
[Route("api/[controller]")]
[ApiController]
public class AuthController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IMapper _mapper;

    public AuthController(IMediator mediator, IMapper mapper)
    {
        _mediator = mediator;
        _mapper = mapper;
    }

    /// <summary>
    /// Creates a kurin together with its first account, which becomes that kurin's Зв'язковий.
    /// </summary>
    /// <remarks>
    /// Admin only, because it is the one call that brings a new kurin into being. Every other registration
    /// attaches a person to a kurin that already exists. The password, the name and the kurin number are
    /// all required: an account minted with a placeholder password is an account whose password is written
    /// in this repository.
    /// </remarks>
    [Authorize(Policy = AuthorizationPolicies.RequireAdmin)]
    [HttpPost("register/kurin")]
    [ProducesResponseType(typeof(RegisterUserResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> RegisterKurin([FromBody] RegisterUserRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Password))
        {
            return this.Failure(ResultType.BadRequest, "PasswordRequired", "A password for the first account is required.");
        }

        if (string.IsNullOrWhiteSpace(request.FirstName) || string.IsNullOrWhiteSpace(request.LastName))
        {
            return this.Failure(ResultType.BadRequest, "NameRequired", "First and last name are required.");
        }

        if (request.KurinNumber is not > 0)
        {
            return this.Failure(ResultType.BadRequest, "KurinNumberRequired", "A positive kurin number is required.");
        }

        var command = new RegisterKurinCommand
        {
            Email = request.Email,
            Password = request.Password,
            FirstName = request.FirstName,
            LastName = request.LastName,
            PhoneNumber = request.PhoneNumber ?? string.Empty,
            KurinNumber = request.KurinNumber.Value
        };
        var response = await _mediator.Send(command);
        return response.ToActionResult(this);
    }

    /// <summary>
    /// Creates an account inside the caller's own kurin.
    /// </summary>
    /// <remarks>
    /// Restricted to kurin management and rate-limited: it is the path an attacker would use to mint
    /// accounts if a management token leaked.
    /// </remarks>
    [Authorize(Policy = AuthorizationPolicies.RequireKurinManagement)]
    [EnableRateLimiting("StrictAuthLimit")]
    [HttpPost("register")]
    [ProducesResponseType(typeof(RegisterUserResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Register([FromBody] RegisterUserRequest request)
    {
        var command = _mapper.Map<RegisterUserCommand>(request);
        var response = await _mediator.Send(command);
        return response.ToActionResult(this);
    }

    /// <summary>
    /// Exchanges an email and password for an access token, and sets the refresh cookie.
    /// </summary>
    /// <remarks>
    /// Answers identically for an unknown address and a wrong password, so the response cannot be used to
    /// discover which addresses are registered. When the account has a second factor, the answer carries no
    /// tokens — finish at <c>mfa/login-verify</c>.
    /// </remarks>
    [AllowAnonymous]
    [EnableRateLimiting("StrictAuthLimit")]
    [HttpPost("login")]
    [ProducesResponseType(typeof(LoginUserResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Login([FromBody] LoginUserRequest request, [FromQuery] bool addAccount = false)
    {
        var command = _mapper.Map<LoginUserCommand>(request);
        command.MfaTrustToken = MfaTrustCookie.Read(Request);
        var response = await _mediator.Send(command);
        if (response.Type == ResultType.Success && response.Data.Tokens != null)
        {
            await KeepPreviousSessionIf(addAccount, response.Data.UserKey);
            SetRefreshTokenCookie(response.Data.Tokens.RefreshToken.Token, response.Data.Tokens.RefreshToken.Expires);
        }
        return response.ToActionResult(this);
    }

    public class SetKurinScopeRequest { public Guid? KurinKey { get; set; } }

    /// <summary>
    /// Points the session at a particular kurin and issues tokens scoped to it.
    /// </summary>
    /// <remarks>
    /// A person may belong to more than one kurin and holds different offices in each, so the token
    /// has to say which one they are acting inside. The handler decides what is allowed: an admin
    /// steps into any kurin or out of all of them, everyone else only into one they belong to.
    /// </remarks>
    [Authorize(Policy = AuthorizationPolicies.RequireUser)]
    [HttpPost("kurin-scope")]
    [ProducesResponseType(typeof(LoginUserResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> SetKurinScope([FromBody] SetKurinScopeRequest request)
    {
        if (this.UserKey() is not { } userKey)
        {
            return this.UnreadableIdentity();
        }

        var command = new SetKurinScopeCommand(userKey, request.KurinKey);
        var response = await _mediator.Send(command);
        if (response.Type == ResultType.Success && response.Data?.Tokens != null)
        {
            SetRefreshTokenCookie(response.Data.Tokens.RefreshToken.Token, response.Data.Tokens.RefreshToken.Expires);
        }

        return response.ToActionResult(this);
    }

    /// <summary>
    /// The kurins this account may currently act in.
    /// </summary>
    /// <remarks>
    /// Answered from the caller's own memberships and nothing else, so it says nothing about
    /// anyone else and needs no permission beyond being signed in.
    /// </remarks>
    [Authorize(Policy = AuthorizationPolicies.RequireUser)]
    [HttpGet("kurin-scope/options")]
    [ProducesResponseType(typeof(IEnumerable<KurinScopeOption>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetKurinScopeOptions()
    {
        if (this.UserKey() is not { } userKey)
        {
            return this.UnreadableIdentity();
        }

        var response = await _mediator.Send(new GetKurinScopeOptionsQuery(userKey));
        return response.ToActionResult(this);
    }

    public class LoadTestLoginRequest { public string ApiKey { get; set; } = string.Empty; }

    /// <summary>
    /// Mints a token for the load-test account without a password.
    /// </summary>
    /// <remarks>
    /// Guarded by <c>LoadTestLoginKey</c>, which ships blank — a blank key disables the endpoint outright.
    /// It has its own secret rather than sharing the rate limiter's bypass key, so letting a monitor past
    /// the limiter cannot also open a login.
    /// </remarks>
    [AllowAnonymous]
    [HttpPost("loadtest-login")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> LoadTestLogin([FromBody] LoadTestLoginRequest request)
    {
        var response = await _mediator.Send(new LoadTestLoginCommand(request.ApiKey));
        return response.ToActionResult(this);
    }

    /// <summary>
    /// Trades the refresh cookie for a fresh access token and rotates the cookie.
    /// </summary>
    /// <remarks>
    /// Walks every refresh cookie the browser sent: a stale cookie from an earlier session otherwise
    /// shadows the current one and logs the user out on reload.
    /// </remarks>
    [AllowAnonymous]
    [HttpPost("refresh")]
    [ProducesResponseType(typeof(RefreshTokenResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Refresh()
    {
        var refreshTokens = GetRefreshTokenCookieValues();
        if (refreshTokens.Count == 0)
        {
            return this.UnreadableIdentity();
        }

        foreach (var refreshToken in refreshTokens.Distinct(StringComparer.Ordinal))
        {
            var command = new RefreshTokenCommand(refreshToken);
            var response = await _mediator.Send(command);
            if (response.Type == ResultType.Success && response.Data?.RefreshToken != null)
            {
                SetRefreshTokenCookie(response.Data.RefreshToken.Token, response.Data.RefreshToken.Expires);
                return response.ToActionResult(this);
            }
        }

        DeleteRefreshTokenCookie();
        return this.UnreadableIdentity();
    }

    /// <summary>
    /// Revokes the caller's refresh token and clears the cookie.
    /// </summary>
    /// <remarks>
    /// Ends the accounts this browser keeps for switching as well: signing out of a shared computer
    /// must not leave another account one click away.
    /// </remarks>
    [Authorize(Policy = AuthorizationPolicies.RequireUser)]
    [HttpPost("logout")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> Logout()
    {
        var refreshTokens = GetRefreshTokenCookieValues();
        var savedTokens = SavedSessionsCookie.Read(Request);
        var command = new LogoutUserCommand(this.UserKey()?.ToString(), refreshTokens, savedTokens);
        var response = await _mediator.Send(command);
        if (refreshTokens.Count > 0)
        {
            DeleteRefreshTokenCookie();
        }
        if (savedTokens.Count > 0)
        {
            SavedSessionsCookie.Delete(HttpContext);
        }
        return response.ToActionResult(this);
    }

    /// <summary>
    /// The other accounts this browser is signed in to, for the account switcher.
    /// </summary>
    /// <remarks>
    /// Anonymous like <c>refresh</c>: the httpOnly cookies are the credential. Sessions that have
    /// ended since are dropped from the cookie on the way.
    /// </remarks>
    [AllowAnonymous]
    [HttpGet("accounts")]
    [ProducesResponseType(typeof(IEnumerable<SavedAccountDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSavedAccounts()
    {
        var savedTokens = SavedSessionsCookie.Read(Request);
        if (savedTokens.Count == 0)
        {
            return Ok(Array.Empty<SavedAccountDto>());
        }

        var response = await _mediator.Send(new GetSavedAccountsQuery(savedTokens, GetRefreshTokenCookieValues()));
        if (response.Type != ResultType.Success || response.Data is null)
        {
            return response.ToActionResult(this);
        }

        if (response.Data.Saved.Count != savedTokens.Count)
        {
            SavedSessionsCookie.Write(HttpContext, response.Data.Saved);
        }
        return Ok(response.Data.Accounts);
    }

    public class SwitchAccountRequest { [JsonRequired] public Guid UserKey { get; set; } }

    /// <summary>
    /// Makes a kept account the one in use; the account that was in use is kept in its place.
    /// </summary>
    /// <remarks>
    /// Answers like a sign-in. Anonymous for the same reason as <c>refresh</c>: the access token of
    /// the account being left may already have expired, and the kept session is what proves the right.
    /// </remarks>
    [AllowAnonymous]
    [HttpPost("accounts/switch")]
    [ProducesResponseType(typeof(LoginUserResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SwitchAccount([FromBody] SwitchAccountRequest request)
    {
        var command = new SwitchSavedAccountCommand(request.UserKey, SavedSessionsCookie.Read(Request), GetRefreshTokenCookieValues());
        var response = await _mediator.Send(command);
        if (response.Type != ResultType.Success || response.Data?.Login.Tokens is not { } tokens)
        {
            return response.ToActionResult(this);
        }

        SetRefreshTokenCookie(tokens.RefreshToken.Token, tokens.RefreshToken.Expires);
        SavedSessionsCookie.Write(HttpContext, response.Data.Saved);
        return Ok(response.Data.Login);
    }

    /// <summary>
    /// Answers whether the caller may act on one named resource, without performing the action.
    /// </summary>
    /// <remarks>
    /// Lets the frontend hide controls the caller cannot use, using the same decision the endpoint itself
    /// would make rather than a second copy of the rules.
    /// </remarks>
    [Authorize(Policy = AuthorizationPolicies.RequireUser)]
    [HttpPost("check-access")]
    [ProducesResponseType(typeof(bool), StatusCodes.Status200OK)]
    public async Task<IActionResult> CheckAccess([FromBody] CheckEntityAccessRequest request)
    {
        var query = new CheckEntityAccessQuery
        {
            EntityType = request.EntityType,
            EntityKey = request.EntityKey,
            Action = request.Action
        };
        var response = await _mediator.Send(query);
        return response.ToActionResult(this);
    }

    /// <summary>
    /// Returns the shared secret and QR payload for enrolling an authenticator app.
    /// </summary>
    /// <remarks>
    /// Enrolment is not finished until <c>mfa/enable</c> confirms a code from that app.
    /// </remarks>
    [Authorize(Policy = AuthorizationPolicies.RequireUser)]
    [EnableRateLimiting("AccountSecurityLimit")]
    [HttpGet("mfa/setup")]
    [ProducesResponseType(typeof(MfaSetupResponseDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMfaSetup()
    {
        if (this.UserKey() is not { } userKey)
        {
            return this.UnreadableIdentity();
        }

        var query = new GetMfaSetupQuery(userKey);
        var response = await _mediator.Send(query);
        return response.ToActionResult(this);
    }

    /// <summary>
    /// Confirms a code from the authenticator app and turns the second factor on.
    /// </summary>
    /// <remarks>
    /// Answers with the recovery codes, which are shown once and never returned again, and with a
    /// fresh session: every other one is ended, and this device's new token already carries the
    /// second factor.
    /// </remarks>
    [Authorize(Policy = AuthorizationPolicies.RequireUser)]
    [EnableRateLimiting("AccountSecurityLimit")]
    [HttpPost("mfa/enable")]
    [ProducesResponseType(typeof(MfaEnableResponseDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> EnableMfa([FromBody] MfaVerifyRequestDto request)
    {
        if (this.UserKey() is not { } userKey)
        {
            return this.UnreadableIdentity();
        }

        var command = new EnableMfaCommand(userKey, request.Code);
        var response = await _mediator.Send(command);
        if (response.Type == ResultType.Success && response.Data?.Tokens != null)
        {
            SetRefreshTokenCookie(response.Data.Tokens.RefreshToken.Token, response.Data.Tokens.RefreshToken.Expires);
        }
        return response.ToActionResult(this);
    }

    /// <summary>
    /// Issues a new set of recovery codes and invalidates the previous set.
    /// </summary>
    [Authorize(Policy = AuthorizationPolicies.RequireUser)]
    [EnableRateLimiting("AccountSecurityLimit")]
    [HttpPost("mfa/recovery-codes")]
    [ProducesResponseType(typeof(MfaRecoveryCodesResponseDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> RotateMfaRecoveryCodes([FromBody] MfaRecoveryCodesRequestDto request)
    {
        if (this.UserKey() is not { } userKey)
        {
            return this.UnreadableIdentity();
        }

        var command = new GenerateMfaRecoveryCodesCommand(userKey, request.CurrentPassword);
        var response = await _mediator.Send(command);
        return response.ToActionResult(this);
    }

    /// <summary>
    /// Completes a sign-in that stopped for the second factor, accepting a code or a recovery code.
    /// </summary>
    /// <remarks>
    /// Anonymous by necessity: the caller has proved the password but has no token yet. What they do hold
    /// is the short-lived <c>mfaToken</c> the password step answered with, and it is required here — a
    /// code on its own is not a sign-in. Carries the account-security rate limit, since it is the point
    /// where codes could be guessed.
    /// </remarks>
    [AllowAnonymous]
    [EnableRateLimiting("AccountSecurityLimit")]
    [HttpPost("mfa/login-verify")]
    [ProducesResponseType(typeof(LoginUserResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> VerifyMfaLogin([FromBody] MfaLoginRequestDto request, [FromQuery] bool addAccount = false)
    {
        var command = new VerifyMfaLoginCommand(request.Email, request.Code, request.RememberMe, request.MfaToken);
        var response = await _mediator.Send(command);
        if (response.Type == ResultType.Success && response.Data.Tokens != null)
        {
            await KeepPreviousSessionIf(addAccount, response.Data.UserKey);
            SetRefreshTokenCookie(response.Data.Tokens.RefreshToken.Token, response.Data.Tokens.RefreshToken.Expires);
        }
        if (response.Type == ResultType.Success && response.Data.MfaTrust is { } trust)
        {
            MfaTrustCookie.Set(HttpContext, trust.Token, trust.ExpiresUtc);
        }
        return response.ToActionResult(this);
    }

    /// <summary>
    /// Reports whether the caller has a second factor, and whether their offices require one.
    /// </summary>
    /// <remarks>
    /// Enforcement is a policy decision, not an account setting: holding a privileged office can make the
    /// second factor mandatory for an account that had it switched off.
    /// </remarks>
    [Authorize(Policy = AuthorizationPolicies.RequireUser)]
    [EnableRateLimiting("AccountSecurityLimit")]
    [HttpGet("mfa/status")]
    [ProducesResponseType(typeof(MfaStatusResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMfaStatus()
    {
        var response = await _mediator.Send(new GetMfaStatusQuery());
        return response.ToActionResult(this);
    }

    /// <summary>
    /// On a sign-in that adds an account, keeps the session the browser was using so the person can
    /// switch back to it. Read from the request before the new refresh cookie replaces it.
    /// </summary>
    private async Task KeepPreviousSessionIf(bool addAccount, Guid signedInUserKey)
    {
        var previous = GetRefreshTokenCookieValues();
        if (!addAccount || previous.Count == 0)
        {
            return;
        }

        var saved = await _mediator.Send(new AddSavedAccountCommand(SavedSessionsCookie.Read(Request), previous, signedInUserKey));
        if (saved.Type == ResultType.Success && saved.Data is not null)
        {
            SavedSessionsCookie.Write(HttpContext, saved.Data);
        }
    }

    private void SetRefreshTokenCookie(string token, DateTime expires) => RefreshTokenCookie.Set(HttpContext, token, expires);

    private void DeleteRefreshTokenCookie() => RefreshTokenCookie.Delete(HttpContext);

    private List<string> GetRefreshTokenCookieValues() => RefreshTokenCookie.Read(Request);
}
