using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProjectK.API.Authorization;
using ProjectK.API.Extensions;
using ProjectK.BusinessLogic.Modules.KurinModule.Features.MemberAward.Pin;

namespace ProjectK.API.Controllers.KurinModule;

/// <summary>
/// The caller's own відзначення, as they want them on their card. There is nobody else to ask about,
/// so being signed in is the whole check; the handler finds the caller's own person.
/// </summary>
[Route("api/me/awards")]
[Authorize(Policy = AuthorizationPolicies.RequireUser)]
[ApiController]
public class MyAwardsController : ControllerBase
{
    private readonly IMediator _mediator;

    public MyAwardsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>Pins a відзначення to the front of the caller's card, or unpins it.</summary>
    [HttpPut("pin")]
    [ProducesResponseType(typeof(bool), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Pin([FromBody] PinMyAwardCommand command)
    {
        var result = await _mediator.Send(command);
        return result.ToActionResult(this);
    }
}
