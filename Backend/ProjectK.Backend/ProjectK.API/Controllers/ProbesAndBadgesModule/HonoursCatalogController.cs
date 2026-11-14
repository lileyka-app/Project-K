using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProjectK.API.Authorization;
using ProjectK.API.Extensions;
using ProjectK.BusinessLogic.Modules.ProbesAndBadgesModule.Features.Catalog;
using ProjectK.Common.Dtos.ProbesAndBadgesModule;

namespace ProjectK.API.Controllers.ProbesAndBadgesModule;

/// <summary>The ступені and senior відзначення with their pictures. Read-only, from the catalogue.</summary>
[Route("api/catalog/honours")]
[Authorize(Policy = AuthorizationPolicies.RequireUser)]
[ApiController]
public class HonoursCatalogController : ControllerBase
{
    private readonly IMediator _mediator;

    public HonoursCatalogController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<CatalogHonour>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll()
    {
        var response = await _mediator.Send(new GetHonoursQuery());
        return response.ToActionResult(this);
    }
}
