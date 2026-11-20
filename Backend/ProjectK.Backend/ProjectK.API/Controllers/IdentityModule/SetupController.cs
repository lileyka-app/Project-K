using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProjectK.API.Extensions;
using ProjectK.BusinessLogic.Modules.IdentityModule.Features.Setup;
using ProjectK.Common.Dtos.IdentityModule;

namespace ProjectK.API.Controllers.IdentityModule;

/// <summary>
/// First-run setup. Anonymous, because on an empty database there is nobody to authenticate as yet.
/// <para>
/// Open in every environment and closed the moment an administrator exists. The seeded tiers
/// therefore never show it; a fresh deployment gets its first administrator here rather than from
/// a password written into the repository, which is what production used to be seeded with.
/// </para>
/// </summary>
[Route("api/auth/setup")]
[ApiController]
public class SetupController : ControllerBase
{
    private readonly IMediator _mediator;

    public SetupController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// Reports whether the instance has been initialised.
    /// </summary>
    /// <remarks>
    /// The frontend reads this before showing a sign-in form, so a fresh instance sends the first visitor
    /// to setup instead of to a login they cannot pass.
    /// </remarks>
    [AllowAnonymous]
    [HttpGet("status")]
    [ProducesResponseType(typeof(SetupStatusDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetStatus()
    {
        var response = await _mediator.Send(new GetSetupStatusQuery());
        return response.ToActionResult(this);
    }

    /// <summary>
    /// Creates the first administrator and closes setup.
    /// </summary>
    /// <remarks>
    /// Refused once an administrator exists, so the endpoint cannot be used to add a second one to a
    /// running instance.
    /// </remarks>
    [AllowAnonymous]
    [HttpPost("initialize")]
    [ProducesResponseType(typeof(LoginUserDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Initialize([FromBody] InitializeSetupCommand command)
    {
        var response = await _mediator.Send(command);
        return response.ToActionResult(this);
    }
}
