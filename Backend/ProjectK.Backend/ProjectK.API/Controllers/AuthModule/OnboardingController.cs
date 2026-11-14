using System.Threading.Tasks;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using ProjectK.API.Authorization;
using ProjectK.API.Cookies;
using ProjectK.API.Extensions;
using ProjectK.BusinessLogic.Modules.AuthModule.Features.Onboarding;
using ProjectK.BusinessLogic.Modules.KraiModule.Features.Krais;
using ProjectK.Common.Dtos.AuthModule;
using ProjectK.Common.Dtos.AuthModule.Requests;
using ProjectK.Common.Dtos.KraiModule;
using ProjectK.Common.Entities.AuthModule;
using ProjectK.Common.Extensions;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Settings;

namespace ProjectK.API.Controllers.AuthModule;

/// <summary>
/// The path from a stranger asking for an account to an activated member: waitlist, invitation,
/// activation, and password reset.
/// </summary>
[Route("api/auth/onboarding")]
[ApiController]
public class OnboardingController : ControllerBase
{
    private readonly IMediator _mediator;

    public OnboardingController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// Records a request for an account. Open to anyone.
    /// </summary>
    /// <remarks>
    /// Creates nothing an applicant can sign in with — an administrator still has to approve the entry,
    /// which is what sends the invitation.
    /// </remarks>
    [AllowAnonymous]
    [HttpPost("waitlist")]
    [ProducesResponseType(typeof(Guid), StatusCodes.Status201Created)]
    public async Task<IActionResult> SubmitWaitlistRegistration([FromBody] SubmitWaitlistRegistrationCommand command)
    {
        var response = await _mediator.Send(command);
        return response.ToActionResult(this);
    }

    /// <summary>
    /// The numbers a kurin may carry, so the forms can say so before the server refuses.
    /// </summary>
    [AllowAnonymous]
    [HttpGet("kurin-numbers")]
    [ProducesResponseType(typeof(KurinNumberRange), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetKurinNumberRange()
    {
        var response = await _mediator.Send(new GetKurinNumberRangeQuery());
        return response.ToActionResult(this);
    }

    /// <summary>
    /// The краї the form offers, by code and name only. Open to anyone, like the form itself.
    /// </summary>
    [AllowAnonymous]
    [HttpGet("krais")]
    [ProducesResponseType(typeof(IReadOnlyList<KraiNameDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetKraiNames()
    {
        var response = await _mediator.Send(new GetKraiNamesQuery());
        return response.ToActionResult(this);
    }

    /// <summary>
    /// Approves a waitlist entry and emails the applicant an invitation link.
    /// </summary>
    /// <remarks>
    /// The body is optional: a край in it overrides the one the applicant picked, and the kurin opens there.
    /// </remarks>
    [Authorize(Policy = AuthorizationPolicies.RequireAdmin)]
    [HttpPost("waitlist/{key:guid}/approve")]
    [ProducesResponseType(typeof(Guid), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ApproveWaitlistEntry(Guid key, [FromBody] ApproveWaitlistEntryRequest? request)
    {
        var response = await _mediator.Send(new ApproveWaitlistEntryCommand(key, request?.KraiCode));
        return response.ToActionResult(this);
    }

    /// <summary>
    /// Sends the invitation again, replacing the previous link.
    /// </summary>
    /// <remarks>
    /// The earlier token stops working, so a link forwarded to the wrong address goes dead.
    /// </remarks>
    [Authorize(Policy = AuthorizationPolicies.RequireAdmin)]
    [HttpPost("waitlist/{key:guid}/resend-invitation")]
    [ProducesResponseType(typeof(Guid), StatusCodes.Status200OK)]
    public async Task<IActionResult> ResendInvitation(Guid key)
    {
        var response = await _mediator.Send(new ResendInvitationCommand(key));
        return response.ToActionResult(this);
    }

    /// <summary>
    /// Re-sends an activation invitation for an account that has not been activated yet.
    /// The response is deliberately generic to avoid account enumeration.
    /// </summary>
    [AllowAnonymous]
    [EnableRateLimiting("AccountSecurityLimit")]
    [HttpPost("invitation/resend")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    public async Task<IActionResult> ResendInvitationByEmail([FromBody] ResendInvitationByEmailCommand command)
    {
        var response = await _mediator.Send(command);
        return response.Type == ResultType.Success
            ? Accepted()
            : response.ToActionResult(this);
    }

    /// <summary>
    /// Rejects a waitlist entry, optionally recording why.
    /// </summary>
    [Authorize(Policy = AuthorizationPolicies.RequireAdmin)]
    [HttpPost("waitlist/{key:guid}/reject")]
    [ProducesResponseType(typeof(Guid), StatusCodes.Status200OK)]
    public async Task<IActionResult> RejectWaitlistEntry(Guid key, [FromBody] string? note)
    {
        var response = await _mediator.Send(new RejectWaitlistEntryCommand(key, note));
        return response.ToActionResult(this);
    }

    /// <summary>
    /// Lists the pending waitlist entries.
    /// </summary>
    [Authorize(Policy = AuthorizationPolicies.RequireAdmin)]
    [HttpGet("waitlist")]
    [ProducesResponseType(typeof(IEnumerable<WaitlistEntry>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetWaitlistEntries()
    {
        var response = await _mediator.Send(new GetWaitlistEntriesQuery());
        return response.ToActionResult(this);
    }

    /// <summary>
    /// Checks an invitation link before the activation form is shown.
    /// </summary>
    /// <remarks>
    /// Anonymous: the recipient has no account yet. Says only whether the token is usable, never who it was
    /// issued to.
    /// </remarks>
    [AllowAnonymous]
    [HttpGet("invitation/{token}/validate")]
    [ProducesResponseType(typeof(InvitationValidationDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> ValidateInvitationToken(string token)
    {
        var response = await _mediator.Send(new ValidateInvitationTokenQuery(token));
        return response.ToActionResult(this);
    }

    /// <summary>
    /// Turns an invitation into a usable account: sets the password and seats the member.
    /// </summary>
    /// <remarks>
    /// The critical path of onboarding — it also attaches the member to their group and office, so a
    /// failure part-way leaves a person who can sign in but belongs nowhere.
    /// </remarks>
    [AllowAnonymous]
    [HttpPost("activate")]
    [ProducesResponseType(typeof(LoginUserDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> ActivateAccount([FromBody] ActivateAccountCommand command)
    {
        var response = await _mediator.Send(command);
        if (response.Type == ResultType.Success && response.Data?.Tokens is { } tokens)
        {
            RefreshTokenCookie.Set(HttpContext, tokens.RefreshToken);
        }

        return response.ToActionResult(this);
    }

    /// <summary>
    /// Emails a password-reset link.
    /// </summary>
    /// <remarks>
    /// Answers the same way whether or not the address is registered, so it cannot be used to enumerate
    /// accounts. Rate-limited for the same reason.
    /// </remarks>
    [AllowAnonymous]
    [EnableRateLimiting("AccountSecurityLimit")]
    [HttpPost("password-reset/request")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> RequestPasswordReset([FromBody] RequestPasswordResetCommand command)
    {
        var response = await _mediator.Send(command);
        return response.ToActionResult(this);
    }

    /// <summary>
    /// Sets a new password from a reset link.
    /// </summary>
    /// <remarks>
    /// The token is single-use and expires; a spent or stale link is refused with
    /// <c>InvalidInvitationToken</c>.
    /// </remarks>
    [AllowAnonymous]
    [EnableRateLimiting("AccountSecurityLimit")]
    [HttpPost("password-reset/reset")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordCommand command)
    {
        var response = await _mediator.Send(command);
        return response.ToActionResult(this);
    }
}
