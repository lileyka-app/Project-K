using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ProjectK.API.Authorization;
using ProjectK.API.Extensions;
using ProjectK.BusinessLogic.Modules.InfrastructureModule.Features.PhoneApp;
using ProjectK.Common.Models.Dtos.InfrastructureModule;

namespace ProjectK.API.Controllers.InfrastructureModule;

/// <summary>
/// The phone app (the PWA under <c>/m/</c>) as the web on a computer offers it.
/// </summary>
[ApiController]
[Route("api/phone-app")]
public class PhoneAppController : ControllerBase
{
    private readonly IMediator _mediator;

    public PhoneAppController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// The install page's address and a QR code of it, for a computer to show to a phone's camera.
    /// </summary>
    [Authorize(Policy = AuthorizationPolicies.RequireUser)]
    [HttpGet("install-code")]
    [ProducesResponseType(typeof(PhoneAppInstallCodeDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetInstallCode(CancellationToken cancellationToken)
    {
        var response = await _mediator.Send(new GetPhoneAppInstallCodeQuery(), cancellationToken);
        return response.ToActionResult(this);
    }
}
