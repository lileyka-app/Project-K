using System.Text.Json.Serialization;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using ProjectK.API.Extensions;
using ProjectK.API.Helpers;
using ProjectK.BusinessLogic.Modules.AuthModule.Features.Demo.Enter;
using ProjectK.BusinessLogic.Modules.AuthModule.Models;
using ProjectK.Common.Models.Enums;

namespace ProjectK.API.Controllers.DemoModule;

/// <summary>
/// The public demo's front door. Absent from the application model everywhere but the Demo
/// environment (<see cref="DemoOnlyControllerFeatureProvider"/>), so a live instance has no such
/// route at all rather than one that refuses.
/// </summary>
[ApiController]
[Route("api/demo")]
public class DemoController : ControllerBase
{
    private readonly IMediator _mediator;

    public DemoController(IMediator mediator)
    {
        _mediator = mediator;
    }

    public sealed record EnterRequest([property: JsonRequired] DemoSeat Seat);

    /// <summary>
    /// Signs the visitor into the demo kurin as the chosen seat. No password; the same strict
    /// limit as a password sign-in, so the demo cannot be used to mint sessions in bulk.
    /// </summary>
    [AllowAnonymous]
    [EnableRateLimiting("StrictAuthLimit")]
    [HttpPost("login")]
    [ProducesResponseType(typeof(LoginUserResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> Enter([FromBody] EnterRequest request, CancellationToken cancellationToken)
    {
        var response = await _mediator.Send(new EnterDemoCommand(request.Seat), cancellationToken);
        if (response.Type == ResultType.Success && response.Data?.Tokens is { } tokens)
        {
            RefreshTokenCookie.Set(HttpContext, tokens.RefreshToken.Token, tokens.RefreshToken.Expires);
        }

        return response.ToActionResult(this);
    }
}
