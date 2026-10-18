using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ProjectK.API.Authorization;
using ProjectK.API.Extensions;
using ProjectK.BusinessLogic.Modules.DuesModule.Features.Krai;
using ProjectK.BusinessLogic.Modules.DuesModule.Models;
using ProjectK.BusinessLogic.Modules.KraiModule.Features.List;
using ProjectK.BusinessLogic.Modules.KraiModule.Features.Profile;
using ProjectK.BusinessLogic.Modules.KraiModule.Models;

namespace ProjectK.API.Controllers.KraiModule;

/// <summary>The краї of this instance. Every kurin belongs to one.</summary>
[Route("api/krai")]
[Authorize(Policy = AuthorizationPolicies.RequireAdmin)]
[ApiController]
public class KraiController : ControllerBase
{
    private readonly IMediator _mediator;

    public KraiController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>Lists the краї with how many kurins each holds.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<KraiResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll()
    {
        var response = await _mediator.Send(new GetKraisQuery());
        return response.ToActionResult(this);
    }

    /// <summary>Returns the край's profile document as it is stored, for the editor and for export.</summary>
    [HttpGet("{kraiCode}/profile")]
    [ProducesResponseType(typeof(KraiProfileEditorResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetProfile(string kraiCode)
    {
        var response = await _mediator.Send(new GetKraiProfileQuery(kraiCode));
        return response.ToActionResult(this);
    }

    /// <summary>
    /// Saves the край's profile as a new revision; an imported file is saved the same way. Refused
    /// with 409 when someone saved after the revision the editor started from.
    /// </summary>
    [HttpPut("{kraiCode}/profile")]
    [ProducesResponseType(typeof(KraiProfileEditorResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> SaveProfile(string kraiCode, [FromBody] SaveKraiProfileRequest request)
    {
        var response = await _mediator.Send(new SaveKraiProfileCommand(kraiCode, request.BaseRevision, request.Document, request.Note));
        return response.ToActionResult(this);
    }

    /// <summary>Lists every saved revision of the край's profile, newest first.</summary>
    [HttpGet("{kraiCode}/profile/revisions")]
    [ProducesResponseType(typeof(IReadOnlyList<KraiProfileRevisionResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetProfileRevisions(string kraiCode)
    {
        var response = await _mediator.Send(new GetKraiProfileRevisionsQuery(kraiCode));
        return response.ToActionResult(this);
    }

    /// <summary>Brings an earlier revision back as the newest one; the history keeps both.</summary>
    [HttpPost("{kraiCode}/profile/revisions/{revision:int}/restore")]
    [ProducesResponseType(typeof(KraiProfileEditorResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RestoreProfileRevision(string kraiCode, int revision)
    {
        var response = await _mediator.Send(new RestoreKraiProfileRevisionCommand(kraiCode, revision));
        return response.ToActionResult(this);
    }

    /// <summary>What each of the край's пільги took off its kurins' вкладка in one quarter.</summary>
    [HttpGet("{kraiCode}/dues/concessions")]
    [ProducesResponseType(typeof(KraiConcessionReportResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetConcessionReport(string kraiCode, [FromQuery] int year, [FromQuery] int number)
    {
        var response = await _mediator.Send(new GetKraiConcessionReportQuery(kraiCode, year, number));
        return response.ToActionResult(this);
    }
}
