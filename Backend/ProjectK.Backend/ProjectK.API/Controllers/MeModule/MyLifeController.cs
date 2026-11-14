using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProjectK.API.Authorization;
using ProjectK.API.Extensions;
using ProjectK.BusinessLogic.Modules.MeModule.Features.Life;
using ProjectK.Common.Dtos.KurinModule;
using ProjectK.Common.Dtos.KurinModule.Requests;
using ProjectK.Common.Models.Enums;

namespace ProjectK.API.Controllers.MeModule;

/// <summary>
/// The person's own life story: the вишколи, табори and заходи they write into it, and the whole
/// story as a PDF. No member key in any route — it is only ever the caller's own.
/// </summary>
[ApiController]
[Route("api/me/life")]
[Authorize(Policy = AuthorizationPolicies.RequireUser)]
public class MyLifeController : ControllerBase
{
    private readonly IMediator _mediator;

    public MyLifeController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<MemberLifeEntryDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetEntries()
    {
        var result = await _mediator.Send(new GetMyLifeEntriesQuery());
        return result.ToActionResult(this);
    }

    [HttpPost]
    [ProducesResponseType(typeof(MemberLifeEntryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> AddEntry([FromBody] SaveLifeEntryRequest request)
    {
        var result = await _mediator.Send(new SaveMyLifeEntryCommand(null, request));
        return result.ToActionResult(this);
    }

    [HttpPut("{entryKey:guid}")]
    [ProducesResponseType(typeof(MemberLifeEntryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateEntry(Guid entryKey, [FromBody] SaveLifeEntryRequest request)
    {
        var result = await _mediator.Send(new SaveMyLifeEntryCommand(entryKey, request));
        return result.ToActionResult(this);
    }

    [HttpDelete("{entryKey:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteEntry(Guid entryKey)
    {
        var result = await _mediator.Send(new DeleteMyLifeEntryCommand(entryKey));
        return result.ToActionResult(this);
    }

    /// <summary>The whole life story as a PDF; <paramref name="timeZone"/> is the reader's IANA zone.</summary>
    [HttpGet("pdf")]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK, "application/pdf")]
    public async Task<IActionResult> ExportPdf([FromQuery] string? timeZone, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new ExportMyLifePdfQuery(timeZone), cancellationToken);
        if (result.Type != ResultType.Success || result.Data is null)
        {
            return result.ToActionResult(this);
        }

        return File(result.Data.Content, "application/pdf", result.Data.FileName);
    }
}
