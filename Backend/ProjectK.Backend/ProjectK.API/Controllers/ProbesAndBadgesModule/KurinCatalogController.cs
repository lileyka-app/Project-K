using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ProjectK.API.Authorization;
using ProjectK.API.Extensions;
using ProjectK.API.Filters;
using ProjectK.BusinessLogic.Modules.ProbesAndBadgesModule.Features.Catalog;
using ProjectK.Common.Dtos.ProbesAndBadgesModule;
using ProjectK.Common.Enums.AuthModule;

namespace ProjectK.API.Controllers.ProbesAndBadgesModule;

/// <summary>
/// The вмілості a kurin is offered, by its край's profile. The whole catalogue stays at
/// api/catalog for looking up anything a member already has.
/// </summary>
[ApiController]
[Route("api/kurin/{kurinKey:guid}/catalog")]
public class KurinCatalogController : ControllerBase
{
    private readonly IMediator _mediator;

    public KurinCatalogController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// Lists the вмілості the kurin's members may start.
    /// </summary>
    [Authorize(Policy = AuthorizationPolicies.RequireUser)]
    [HttpGet("badges")]
    [ResourceAuthorize(ResourceType.Kurin, ResourceAction.Read, "route:kurinKey")]
    [ProducesResponseType(typeof(IEnumerable<Badge>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetBadges(Guid kurinKey)
    {
        var response = await _mediator.Send(new GetKurinBadgesQuery(kurinKey));
        return response.ToActionResult(this);
    }
}
