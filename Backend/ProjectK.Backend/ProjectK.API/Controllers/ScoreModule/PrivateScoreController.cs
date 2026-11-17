using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProjectK.API.Authorization;
using ProjectK.API.Extensions;
using ProjectK.API.Filters;
using ProjectK.BusinessLogic.Modules.ScoreModule.Features.Private;
using ProjectK.Common.Dtos.ScoreModule;
using ProjectK.Common.Dtos.ScoreModule.Requests;
using ProjectK.Common.Enums.IdentityModule;

namespace ProjectK.API.Controllers.ScoreModule;

/// <summary>
/// The КВ's own score — a book the Звʼязковий and the впорядники keep among themselves. Every route
/// is behind <see cref="ResourceType.KurinScorePrivate"/>, which nobody else holds at any scope.
/// </summary>
[ApiController]
[Route("api/kurin/{kurinKey:guid}/score/private")]
[Authorize(Policy = AuthorizationPolicies.RequireUser)]
public class PrivateScoreController : ControllerBase
{
    private readonly IMediator _mediator;

    public PrivateScoreController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet]
    [ResourceAuthorize(ResourceType.KurinScorePrivate, ResourceAction.Read, "route:kurinKey")]
    [ProducesResponseType(typeof(PrivateScoreDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> Get(Guid kurinKey, [FromQuery] ScorePeriodQuery period)
    {
        var result = await _mediator.Send(new GetPrivateScoreQuery(kurinKey, period));
        return result.ToActionResult(this);
    }

    [HttpPost("entries")]
    [ResourceAuthorize(ResourceType.KurinScorePrivate, ResourceAction.Create, "route:kurinKey")]
    [ProducesResponseType(typeof(Guid), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateEntry(Guid kurinKey, [FromBody] UpsertPrivateScoreEntryRequest request)
    {
        var result = await _mediator.Send(new CreatePrivateScoreEntryCommand(kurinKey, request));
        return result.ToActionResult(this);
    }

    [HttpPut("entries/{entryKey:guid}")]
    [ResourceAuthorize(ResourceType.KurinScorePrivate, ResourceAction.Update, "route:kurinKey")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateEntry(Guid kurinKey, Guid entryKey, [FromBody] UpsertPrivateScoreEntryRequest request)
    {
        var result = await _mediator.Send(new UpdatePrivateScoreEntryCommand(kurinKey, entryKey, request));
        return result.ToActionResult(this);
    }

    [HttpDelete("entries/{entryKey:guid}")]
    [ResourceAuthorize(ResourceType.KurinScorePrivate, ResourceAction.Delete, "route:kurinKey")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteEntry(Guid kurinKey, Guid entryKey)
    {
        var result = await _mediator.Send(new DeletePrivateScoreEntryCommand(kurinKey, entryKey));
        return result.ToActionResult(this);
    }

    [HttpPost("criteria")]
    [ResourceAuthorize(ResourceType.KurinScorePrivate, ResourceAction.Update, "route:kurinKey")]
    [ProducesResponseType(typeof(Guid), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateCriterion(Guid kurinKey, [FromBody] UpsertPrivateScoreCriterionRequest request)
    {
        var result = await _mediator.Send(new UpsertPrivateScoreCriterionCommand(kurinKey, null, request));
        return result.ToActionResult(this);
    }

    [HttpPut("criteria/{criterionKey:guid}")]
    [ResourceAuthorize(ResourceType.KurinScorePrivate, ResourceAction.Update, "route:kurinKey")]
    [ProducesResponseType(typeof(Guid), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateCriterion(Guid kurinKey, Guid criterionKey, [FromBody] UpsertPrivateScoreCriterionRequest request)
    {
        var result = await _mediator.Send(new UpsertPrivateScoreCriterionCommand(kurinKey, criterionKey, request));
        return result.ToActionResult(this);
    }
}
