using System.Text.Json.Serialization;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ProjectK.API.Authorization;
using ProjectK.API.Extensions;
using ProjectK.API.Helpers;
using ProjectK.BusinessLogic.Modules.AuthModule.Features.DevTools.Impersonate;
using ProjectK.BusinessLogic.Modules.AuthModule.Features.DevTools.ImpersonateMember;
using ProjectK.BusinessLogic.Modules.AuthModule.Features.DevTools.Return;
using ProjectK.BusinessLogic.Modules.AuthModule.Models;
using ProjectK.Common.Models.Enums;

namespace ProjectK.API.Controllers.DevModule;

/// <summary>
/// Testing aids for the local tiers. Absent from the application model everywhere else
/// (<see cref="DevOnlyControllerFeatureProvider"/>), the way the e2e fixtures are, so that in
/// Production these routes do not exist rather than merely refuse.
/// </summary>
[Route("api/dev")]
[ApiController]
public class DevToolsController : ControllerBase
{
    private readonly IMediator _mediator;

    public DevToolsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    public record ImpersonateRequest([property: JsonRequired] DevRole Role, Guid? KurinKey);

    public record ImpersonateMemberRequest([property: JsonRequired] Guid MemberKey);

    public record ReturnRequest(string Ticket);

    /// <summary>
    /// Signs the administrator in as somebody holding the role in the current kurin. Administrators only.
    /// </summary>
    /// <remarks>
    /// The answer carries a return ticket; keep it, <c>POST api/dev/return</c> with it brings the
    /// administrator back and ends the borrowed session.
    /// </remarks>
    [Authorize(Policy = AuthorizationPolicies.RequireAdmin)]
    [HttpPost("impersonate")]
    [ProducesResponseType(typeof(DevImpersonationResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Impersonate([FromBody] ImpersonateRequest request)
    {
        var response = await _mediator.Send(new ImpersonateRoleCommand(request.Role, request.KurinKey));
        if (response.Type == ResultType.Success && response.Data?.Login.Tokens is { } tokens)
        {
            RefreshTokenCookie.Set(HttpContext, tokens.RefreshToken.Token, tokens.RefreshToken.Expires);
        }

        return response.ToActionResult(this);
    }

    /// <summary>
    /// Signs the administrator in as one particular member: the one whose card is open. Administrators only.
    /// </summary>
    [Authorize(Policy = AuthorizationPolicies.RequireAdmin)]
    [HttpPost("impersonate/member")]
    [ProducesResponseType(typeof(DevImpersonationResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ImpersonateMember([FromBody] ImpersonateMemberRequest request)
    {
        var response = await _mediator.Send(new ImpersonateMemberCommand(request.MemberKey));
        if (response.Type == ResultType.Success && response.Data?.Login.Tokens is { } tokens)
        {
            RefreshTokenCookie.Set(HttpContext, tokens.RefreshToken.Token, tokens.RefreshToken.Expires);
        }

        return response.ToActionResult(this);
    }

    /// <summary>
    /// Steps back to the administrator named by the ticket and ends the borrowed session.
    /// </summary>
    /// <remarks>
    /// Anonymous on purpose: the caller is signed in as the borrowed account, which is not an
    /// administrator. The ticket is the credential.
    /// </remarks>
    [AllowAnonymous]
    [HttpPost("return")]
    [ProducesResponseType(typeof(LoginUserResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Return([FromBody] ReturnRequest request)
    {
        var response = await _mediator.Send(new ReturnFromImpersonationCommand(request.Ticket, RefreshTokenCookie.Read(Request)));
        if (response.Type == ResultType.Success && response.Data?.Tokens is { } tokens)
        {
            RefreshTokenCookie.Set(HttpContext, tokens.RefreshToken.Token, tokens.RefreshToken.Expires);
        }

        return response.ToActionResult(this);
    }
}
