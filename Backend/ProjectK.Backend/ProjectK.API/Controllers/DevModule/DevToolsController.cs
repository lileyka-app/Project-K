using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ProjectK.API.Authorization;
using ProjectK.API.Cookies;
using ProjectK.API.Extensions;
using ProjectK.API.Hosting;
using ProjectK.BusinessLogic.Modules.AuthModule.Features.DevTools;
using ProjectK.Common.Dtos.AuthModule;
using ProjectK.Common.Dtos.AuthModule.Requests;
using ProjectK.Common.Enums.Shared;

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

    /// <summary>
    /// Signs the administrator in as somebody holding the role in the current kurin. Administrators only.
    /// </summary>
    /// <remarks>
    /// The answer carries a return ticket; keep it, <c>POST api/dev/return</c> with it brings the
    /// administrator back and ends the borrowed session.
    /// </remarks>
    [Authorize(Policy = AuthorizationPolicies.RequireAdmin)]
    [HttpPost("impersonate")]
    [ProducesResponseType(typeof(DevImpersonationDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Impersonate([FromBody] ImpersonateRoleRequest request)
    {
        var response = await _mediator.Send(new ImpersonateRoleCommand(request.Role, request.KurinKey));
        if (response.Type == ResultType.Success && response.Data?.Login.Tokens is { } tokens)
        {
            RefreshTokenCookie.Set(HttpContext, tokens.RefreshToken);
        }

        return response.ToActionResult(this);
    }

    /// <summary>
    /// Signs the administrator in as one particular member: the one whose card is open. Administrators only.
    /// </summary>
    [Authorize(Policy = AuthorizationPolicies.RequireAdmin)]
    [HttpPost("impersonate/member")]
    [ProducesResponseType(typeof(DevImpersonationDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ImpersonateMember([FromBody] ImpersonateMemberRequest request)
    {
        var response = await _mediator.Send(new ImpersonateMemberCommand(request.MemberKey));
        if (response.Type == ResultType.Success && response.Data?.Login.Tokens is { } tokens)
        {
            RefreshTokenCookie.Set(HttpContext, tokens.RefreshToken);
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
    [ProducesResponseType(typeof(LoginUserDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Return([FromBody] ReturnFromImpersonationRequest request)
    {
        var response = await _mediator.Send(new ReturnFromImpersonationCommand(request.Ticket, RefreshTokenCookie.Read(Request)));
        if (response.Type == ResultType.Success && response.Data?.Tokens is { } tokens)
        {
            RefreshTokenCookie.Set(HttpContext, tokens.RefreshToken);
        }

        return response.ToActionResult(this);
    }
}
