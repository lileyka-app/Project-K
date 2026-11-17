using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using ProjectK.API.Authorization;
using ProjectK.API.Extensions;
using ProjectK.BusinessLogic.Modules.MemberModule.Features.Card;
using ProjectK.Common.Dtos.MemberModule;
using ProjectK.Common.Dtos.MemberModule.Requests;

namespace ProjectK.API.Controllers.MemberModule;

/// <summary>
/// The caller's own shared card and the codes they show: the card's fields and link, the «перевірити»
/// QR and the transfer code. Being signed in is the whole check. Others read the card through
/// <see cref="PublicCardController"/>.
/// </summary>
[ApiController]
[Route("api/me")]
[Authorize(Policy = AuthorizationPolicies.RequireUser)]
public class MyCardController : ControllerBase
{
    private readonly IMediator _mediator;

    public MyCardController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>The person's shared card: what they may show, what they chose, the permanent link and a preview.</summary>
    [HttpGet("card")]
    [ProducesResponseType(typeof(MyCardDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetCard()
    {
        var result = await _mediator.Send(new GetMyCardQuery());
        return result.ToActionResult(this);
    }

    [HttpPut("card/fields")]
    [ProducesResponseType(typeof(MyCardDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> SetCardFields([FromBody] CardFieldsRequest request)
    {
        var result = await _mediator.Send(new SetMyCardFieldsCommand(request.Fields));
        return result.ToActionResult(this);
    }

    /// <summary>Makes a permanent card link, replacing the old one.</summary>
    [HttpPost("card/link")]
    [EnableRateLimiting("AccountSecurityLimit")]
    [ProducesResponseType(typeof(MyCardDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> IssueCardLink()
    {
        var result = await _mediator.Send(new IssueMyCardLinkCommand());
        return result.ToActionResult(this);
    }

    [HttpDelete("card/link")]
    [ProducesResponseType(typeof(MyCardDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> DisableCardLink()
    {
        var result = await _mediator.Send(new DisableMyCardLinkCommand());
        return result.ToActionResult(this);
    }

    /// <summary>A QR to check the card by, working for three minutes.</summary>
    [HttpGet("card/check-code")]
    [ProducesResponseType(typeof(CardCodeDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetCardCheckCode()
    {
        var result = await _mediator.Send(new GetMyCardCheckCodeQuery());
        return result.ToActionResult(this);
    }

    /// <summary>The transfer code as a QR that opens «Склад куреня» with the code typed in.</summary>
    [HttpGet("transfer-code")]
    [ProducesResponseType(typeof(CardCodeDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetTransferCode()
    {
        var result = await _mediator.Send(new GetMyTransferCodeQuery());
        return result.ToActionResult(this);
    }
}
