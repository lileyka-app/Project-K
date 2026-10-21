using System.Threading.Tasks;
using MediatR;
using ProjectK.API.Helpers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using ProjectK.API.Authorization;
using ProjectK.API.Extensions;
using ProjectK.BusinessLogic.Modules.AuthModule.Features.Onboarding.ActivateAccount;
using ProjectK.BusinessLogic.Modules.AuthModule.Features.Onboarding.ApproveWaitlistEntry;
using ProjectK.BusinessLogic.Modules.AuthModule.Features.Onboarding.GetWaitlistEntries;
using ProjectK.BusinessLogic.Modules.AuthModule.Features.Onboarding.RejectWaitlistEntry;
using ProjectK.BusinessLogic.Modules.AuthModule.Features.Onboarding.RequestPasswordReset;
using ProjectK.BusinessLogic.Modules.AuthModule.Features.Onboarding.ResendInvitation;
using ProjectK.BusinessLogic.Modules.AuthModule.Features.Onboarding.ResetPassword;
using ProjectK.BusinessLogic.Modules.AuthModule.Features.Onboarding.SubmitWaitlistRegistration;
using ProjectK.BusinessLogic.Modules.AuthModule.Features.Onboarding.ValidateInvitationToken;
using ProjectK.BusinessLogic.Modules.AuthModule.Models;
using ProjectK.BusinessLogic.Modules.KraiModule.Features.List;
using ProjectK.BusinessLogic.Modules.KraiModule.Models;
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
    public IActionResult GetKurinNumberRange([FromServices] IOptions<KurinNumberOptions> numbers)
        => Ok(new KurinNumberRange(KurinNumberOptions.MinNumber, numbers.Value.MaxNumber));

    /// <summary>
    /// The краї the form offers, by code and name only. Open to anyone, like the form itself.
    /// </summary>
    [AllowAnonymous]
    [HttpGet("krais")]
    [ProducesResponseType(typeof(IReadOnlyList<KraiNameResponse>), StatusCodes.Status200OK)]
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
    [HttpPost("waitlist/{key}/approve")]
    [ProducesResponseType(typeof(Guid), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ApproveWaitlistEntry(Guid key, [FromBody] ApproveWaitlistEntryRequest? request)
    {
        var response = await _mediator.Send(new ApproveWaitlistEntryCommand(key, request?.KraiCode));
        return response.ToActionResult(this);
    }

    /// <summary>Body of <c>POST waitlist/{key}/approve</c>.</summary>
    public sealed class ApproveWaitlistEntryRequest
    {
        public string? KraiCode { get; set; }
    }

    /// <summary>
    /// Sends the invitation again, replacing the previous link.
    /// </summary>
    /// <remarks>
    /// The earlier token stops working, so a link forwarded to the wrong address goes dead.
    /// </remarks>
    [Authorize(Policy = AuthorizationPolicies.RequireAdmin)]
    [HttpPost("waitlist/{key}/resend-invitation")]
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
    [ProducesResponseType(typeof(bool), StatusCodes.Status202Accepted)]
    public async Task<IActionResult> ResendInvitationByEmail([FromBody] ResendInvitationByEmailCommand command)
    {
        var response = await _mediator.Send(command);
        return response.Type == ResultType.Success
            ? Accepted(response.Data)
            : response.ToActionResult(this);
    }

    /// <summary>
    /// Rejects a waitlist entry, optionally recording why.
    /// </summary>
    [Authorize(Policy = AuthorizationPolicies.RequireAdmin)]
    [HttpPost("waitlist/{key}/reject")]
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
    [ProducesResponseType(typeof(InvitationValidationResponse), StatusCodes.Status200OK)]
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
    [ProducesResponseType(typeof(LoginUserResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> ActivateAccount([FromBody] ActivateAccountCommand command)
    {
        var response = await _mediator.Send(command);
        if (response.Type == ResultType.Success && response.Data?.Tokens is { } tokens)
        {
            RefreshTokenCookie.Set(HttpContext, tokens.RefreshToken.Token, tokens.RefreshToken.Expires);
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
    [ProducesResponseType(typeof(bool), StatusCodes.Status200OK)]
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
    [ProducesResponseType(typeof(bool), StatusCodes.Status200OK)]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordCommand command)
    {
        var response = await _mediator.Send(command);
        return response.ToActionResult(this);
    }
}
