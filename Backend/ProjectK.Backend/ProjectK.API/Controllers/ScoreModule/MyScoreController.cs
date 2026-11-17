using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProjectK.API.Authorization;
using ProjectK.API.Extensions;
using ProjectK.BusinessLogic.Modules.ScoreModule.Features.Member;
using ProjectK.Common.Dtos.ScoreModule;

namespace ProjectK.API.Controllers.ScoreModule;

/// <summary>The caller's own points. Being signed in is the whole check.</summary>
[ApiController]
[Route("api/me")]
[Authorize(Policy = AuthorizationPolicies.RequireUser)]
public class MyScoreController : ControllerBase
{
    private readonly IMediator _mediator;

    public MyScoreController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>Own points this пластовий рік, per kurin where the person is a youth in a гурток.</summary>
    [HttpGet("score")]
    [ProducesResponseType(typeof(IReadOnlyList<MyScoreDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetScore()
    {
        var result = await _mediator.Send(new GetMyScoreQuery());
        return result.ToActionResult(this);
    }
}
