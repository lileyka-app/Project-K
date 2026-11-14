using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using ProjectK.BusinessLogic.Modules.MeModule.Features.Card;
using ProjectK.Common.Dtos.MeModule;
using ProjectK.Common.Enums.Shared;
using ProjectK.Common.Rules.Shared;

namespace ProjectK.API.Controllers.MeModule;

/// <summary>
/// Shared member cards as anyone sees them, signed in or not: the one behind a «перевірити» QR and
/// the one behind a permanent link. The token in the path is the whole of the access, so the request
/// log masks it (<see cref="SecretPathRules.PublicCardPrefix"/>), and nothing is cached.
/// </summary>
[ApiController]
public class PublicCardController : ControllerBase
{
    private readonly IMediator _mediator;

    public PublicCardController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [AllowAnonymous]
    [HttpGet(SecretPathRules.PublicCardPrefix + "check/{token}")]
    [EnableRateLimiting("PublicCardLimit")]
    [ProducesResponseType(typeof(PublicCardDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Checked(string token, CancellationToken cancellationToken) =>
        Answer(await _mediator.Send(new ReadCheckedCardQuery(token), cancellationToken));

    [AllowAnonymous]
    [HttpGet(SecretPathRules.PublicCardPrefix + "link/{token}")]
    [EnableRateLimiting("PublicCardLimit")]
    [ProducesResponseType(typeof(PublicCardDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Linked(string token, CancellationToken cancellationToken) =>
        Answer(await _mediator.Send(new ReadLinkedCardQuery(token), cancellationToken));

    private IActionResult Answer(Common.Records.Shared.ServiceResult<PublicCardDto> result)
    {
        Response.Headers.CacheControl = "no-store";
        Response.Headers["X-Robots-Tag"] = "noindex, nofollow";
        // Nothing in the body on failure: an unknown, expired or withdrawn card all look the same.
        return result.Type == ResultType.Success && result.Data is not null ? Ok(result.Data) : NotFound();
    }
}
