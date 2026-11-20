using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProjectK.API.Authorization;
using ProjectK.API.Extensions;
using ProjectK.BusinessLogic.Modules.DuesModule.Features.Member;
using ProjectK.Common.Dtos.DuesModule;

namespace ProjectK.API.Controllers.DuesModule;

/// <summary>The caller's own внески, in every kurin that charges them. Being signed in is the whole check.</summary>
[ApiController]
[Route("api/me")]
[Authorize(Policy = AuthorizationPolicies.RequireUser)]
public class MyDuesController : ControllerBase
{
    private readonly IMediator _mediator;

    public MyDuesController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>The вкладка in every kurin that charges the person.</summary>
    [HttpGet("dues")]
    [ProducesResponseType(typeof(IReadOnlyList<MyDuesDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetDues()
    {
        var result = await _mediator.Send(new GetMyDuesQuery());
        return result.ToActionResult(this);
    }
}
