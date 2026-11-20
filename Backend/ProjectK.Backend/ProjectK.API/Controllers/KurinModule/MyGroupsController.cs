using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProjectK.API.Authorization;
using ProjectK.API.Extensions;
using ProjectK.BusinessLogic.Modules.KurinModule.Features.Group;
using ProjectK.Common.Dtos.KurinModule;

namespace ProjectK.API.Controllers.KurinModule;

/// <summary>The гуртки the caller is in or leads. Being signed in is the whole check.</summary>
[ApiController]
[Route("api/me")]
[Authorize(Policy = AuthorizationPolicies.RequireUser)]
public class MyGroupsController : ControllerBase
{
    private readonly IMediator _mediator;

    public MyGroupsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>The гуртки the person is in or leads, in every kurin, with their сильветки.</summary>
    [HttpGet("groups")]
    [ProducesResponseType(typeof(IReadOnlyList<MyGroupDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetGroups()
    {
        var result = await _mediator.Send(new GetMyGroupsQuery());
        return result.ToActionResult(this);
    }
}
