using System.Threading.Tasks;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProjectK.API.Authorization;
using ProjectK.API.Extensions;
using ProjectK.BusinessLogic.Modules.AuthModule.Features.Migration;
using ProjectK.Common.Dtos.AuthModule;
using ProjectK.Common.Extensions;

namespace ProjectK.API.Controllers.AuthModule;

/// <summary>
/// Read-only reporting on data that predates the office-based role model. Admin only.
/// </summary>
[Authorize(Policy = AuthorizationPolicies.RequireAdmin)]
[Route("api/auth/migration")]
[ApiController]
public class MigrationController : ControllerBase
{
    private readonly IMediator _mediator;

    public MigrationController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// Reports what the legacy role migration would change, without changing anything.
    /// </summary>
    [HttpGet("preflight")]
    [ProducesResponseType(typeof(MigrationPreflightReportDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPreflightReport()
    {
        var query = new GetMigrationPreflightReportQuery();
        var response = await _mediator.Send(query);
        return response.ToActionResult(this);
    }
}
