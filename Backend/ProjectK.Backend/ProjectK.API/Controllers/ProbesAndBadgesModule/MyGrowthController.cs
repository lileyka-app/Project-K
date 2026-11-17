using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProjectK.API.Authorization;
using ProjectK.API.Extensions;
using ProjectK.BusinessLogic.Modules.ProbesAndBadgesModule.Features.Growth;
using ProjectK.Common.Dtos.ProbesAndBadgesModule;

namespace ProjectK.API.Controllers.ProbesAndBadgesModule;

/// <summary>The caller's own проба and вмілості. Being signed in is the whole check.</summary>
[ApiController]
[Route("api/me")]
[Authorize(Policy = AuthorizationPolicies.RequireUser)]
public class MyGrowthController : ControllerBase
{
    private readonly IMediator _mediator;

    public MyGrowthController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>The проба in hand and the вмілості — empty, with the flag off, for anyone outside the youth programme.</summary>
    [HttpGet("growth")]
    [ProducesResponseType(typeof(MyGrowthDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetGrowth()
    {
        var result = await _mediator.Send(new GetMyGrowthQuery());
        return result.ToActionResult(this);
    }
}
