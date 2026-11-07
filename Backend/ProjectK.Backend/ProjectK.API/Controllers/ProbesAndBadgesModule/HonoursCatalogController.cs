using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProjectK.API.Authorization;
using ProjectK.Common.Interfaces.Modules.ProbesAndBadgesModule;
using ProjectK.Common.Models.Catalog;

namespace ProjectK.API.Controllers.ProbesAndBadgesModule;

/// <summary>The ступені and senior відзначення with their pictures. Read-only, from the catalogue.</summary>
[Route("api/catalog/honours")]
[Authorize(Policy = AuthorizationPolicies.RequireUser)]
[ApiController]
public class HonoursCatalogController : ControllerBase
{
    private readonly IHonoursCatalog _catalog;

    public HonoursCatalogController(IHonoursCatalog catalog)
    {
        _catalog = catalog;
    }

    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<CatalogHonour>), StatusCodes.Status200OK)]
    public IActionResult GetAll() => Ok(_catalog.GetAll());
}
