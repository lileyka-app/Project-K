using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProjectK.API.Authorization;
using ProjectK.API.Extensions;
using ProjectK.BusinessLogic.Modules.IdentityModule.Features.Duties;
using ProjectK.Common.Dtos.IdentityModule;
using ProjectK.Common.Dtos.IdentityModule.Requests;
using ProjectK.Common.Enums.Shared;

namespace ProjectK.API.Controllers.IdentityModule;

/// <summary>
/// «Справи»: what waits on the caller as провід, collected from every module that gives duties, and
/// what they hid. Being signed in is the whole check; each kurin's rights are settled inside.
/// </summary>
[ApiController]
[Route("api/me")]
[Authorize(Policy = AuthorizationPolicies.RequireUser)]
public class MyDutiesController : ControllerBase
{
    private readonly IMediator _mediator;

    public MyDutiesController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// What waits on the person as провід, as a bare list, less what they hid. Kept in this shape for
    /// web tabs and phone bundles from before 1.2.3; current clients read <see cref="GetDutiesBoard"/>.
    /// </summary>
    [HttpGet("duties")]
    [ProducesResponseType(typeof(IReadOnlyList<MyDutyDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetDuties()
    {
        var result = await _mediator.Send(new GetMyDutiesQuery());
        return result.Type == ResultType.Success ? Ok(result.Data!.Items) : result.ToActionResult(this);
    }

    /// <summary>What waits on the person as провід, per kurin where they hold an office, less what they hid, and how many they hid.</summary>
    [HttpGet("duties/board")]
    [ProducesResponseType(typeof(MyDutiesDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetDutiesBoard()
    {
        var result = await _mediator.Send(new GetMyDutiesQuery());
        return result.ToActionResult(this);
    }

    /// <summary>Hides one duty from the person's own «Справи» until it grows.</summary>
    [HttpPost("duties/dismissed")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> DismissDuty([FromBody] DismissDutyRequest request)
    {
        var result = await _mediator.Send(new DismissDutyCommand(request.Key, request.Fingerprint));
        return result.ToActionResult(this);
    }

    /// <summary>Brings hidden duties back: the one under <paramref name="key"/>, or all of them without it.</summary>
    [HttpDelete("duties/dismissed")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> RestoreDuties([FromQuery] string? key = null)
    {
        var result = await _mediator.Send(new RestoreDutiesCommand(key));
        return result.ToActionResult(this);
    }
}
