using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ProjectK.API.Authorization;
using ProjectK.API.Extensions;
using ProjectK.BusinessLogic.Modules.ProbesAndBadgesModule.Features.Catalog;
using ProjectK.Common.Models.Catalog;

namespace ProjectK.API.Controllers.ProbesAndBadgesModule;

/// <summary>
/// The badge catalogue — the definitions themselves, identical for every kurin. Read-only: the
/// catalogue service owns them and Lilejka keeps a copy.
/// </summary>
[Route("api/catalog/badges")]
[Authorize(Policy = AuthorizationPolicies.RequireUser)]
[ApiController]
public class BadgesCatalogController : ControllerBase
{
    private readonly IMediator _mediator;

    public BadgesCatalogController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// Returns the catalogue's categories and levels, for building filters.
    /// </summary>
    [HttpGet("meta")]
    [ProducesResponseType(typeof(BadgesMetadata), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMetadata()
    {
        var response = await _mediator.Send(new GetBadgesMetadataQuery());
        return response.ToActionResult(this);
    }

    /// <summary>
    /// Lists badge definitions.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<Badge>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll([FromQuery] int take = GetBadgesQueryHandler.DefaultPage)
    {
        var response = await _mediator.Send(new GetBadgesQuery(take));
        return response.ToActionResult(this);
    }

    /// <summary>
    /// Returns one badge definition.
    /// </summary>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(Badge), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(string id)
    {
        var response = await _mediator.Send(new GetBadgeQuery(id));
        return response.ToActionResult(this);
    }
}
