using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProjectK.API.Authorization;
using ProjectK.API.Extensions;
using ProjectK.BusinessLogic.Modules.ProbesAndBadgesModule.Features.Catalog;
using ProjectK.Common.Models.Dtos.ProbesAndBadgesModule;

namespace ProjectK.API.Controllers.ProbesAndBadgesModule;

/// <summary>
/// The probe catalogue — read-only definitions shared by every kurin.
/// </summary>
[Route("api/catalog/probes")]
[Authorize(Policy = AuthorizationPolicies.RequireUser)]
[ApiController]
public class ProbesCatalogController : ControllerBase
{
    private readonly IMediator _mediator;

    public ProbesCatalogController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// Lists probe definitions.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<ProbeSummaryDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll()
    {
        var response = await _mediator.Send(new GetProbesQuery());
        return response.ToActionResult(this);
    }

    /// <summary>
    /// Returns one probe with its points grouped as they are presented in the book.
    /// </summary>
    [HttpGet("{probeId}/grouped")]
    [ProducesResponseType(typeof(GroupedProbeDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetGroupedById(string probeId)
    {
        var response = await _mediator.Send(new GetGroupedProbeQuery(probeId));
        return response.ToActionResult(this);
    }
}
