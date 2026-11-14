using System.Globalization;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProjectK.API.Authorization;
using ProjectK.API.Extensions;
using ProjectK.API.Filters;
using ProjectK.BusinessLogic.Modules.ScoreModule;
using ProjectK.BusinessLogic.Modules.ScoreModule.Features.Entry;
using ProjectK.BusinessLogic.Modules.ScoreModule.Features.Group;
using ProjectK.BusinessLogic.Modules.ScoreModule.Features.Kurin;
using ProjectK.BusinessLogic.Modules.ScoreModule.Features.Settings;
using ProjectK.BusinessLogic.Modules.ScoreModule.Features.Sheet;
using ProjectK.Common.Models.Dtos.ScoreModule;
using ProjectK.Common.Models.Dtos.ScoreModule.Requests;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Records;

namespace ProjectK.API.Controllers.ScoreModule;

/// <summary>
/// Точкування of a kurin: the table everyone sees, a гурток's page, the sheet of one event, points
/// given by hand, and the rules. Every route is keyed by the kurin; what a caller may do inside it —
/// which гурток they score — is the handlers' question, asked per person and per гурток.
/// </summary>
[ApiController]
[Route("api/kurin/{kurinKey:guid}/score")]
[Authorize(Policy = AuthorizationPolicies.RequireUser)]
public class KurinScoreController : ControllerBase
{
    private readonly IMediator _mediator;

    public KurinScoreController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>The table of гуртки — any member of the kurin.</summary>
    [HttpGet]
    [ResourceAuthorize(ResourceType.KurinScore, ResourceAction.Read, "route:kurinKey")]
    [ProducesResponseType(typeof(KurinScoreDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> Get(Guid kurinKey, [FromQuery] ScorePeriodQuery period)
    {
        var result = await _mediator.Send(new GetKurinScoreQuery(kurinKey, period));
        return result.ToActionResult(this);
    }

    [HttpGet("groups/{groupKey:guid}")]
    [ResourceAuthorize(ResourceType.GroupScore, ResourceAction.Read, "route:groupKey")]
    [ProducesResponseType(typeof(GroupScoreDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetGroup(Guid kurinKey, Guid groupKey, [FromQuery] ScorePeriodQuery period)
    {
        var result = await _mediator.Send(new GetGroupScoreQuery(groupKey, period));
        return result.ToActionResult(this);
    }

    /// <summary>Who may score at least one гурток opens it; the handler says no to everyone else.</summary>
    [HttpGet("events/{agendaItemKey:guid}/{occurrence}")]
    [ResourceAuthorize(ResourceType.KurinScore, ResourceAction.Read, "route:kurinKey")]
    [ProducesResponseType(typeof(AttendanceSheetDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public Task<IActionResult> GetSheet(Guid kurinKey, Guid agendaItemKey, string occurrence) =>
        SendAtOccurrenceAsync(occurrence, startUtc => new GetAttendanceSheetQuery(kurinKey, agendaItemKey, startUtc));

    [HttpPost("events/{agendaItemKey:guid}/{occurrence}/attendance")]
    [ResourceAuthorize(ResourceType.KurinScore, ResourceAction.Read, "route:kurinKey")]
    [ProducesResponseType(typeof(IReadOnlyList<MarkAttendanceResultDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public Task<IActionResult> MarkAttendance(Guid kurinKey, Guid agendaItemKey, string occurrence, [FromBody] MarkAttendanceRequest request) =>
        SendAtOccurrenceAsync(occurrence, startUtc => new MarkAttendanceCommand(kurinKey, agendaItemKey, startUtc, request));

    [HttpDelete("events/{agendaItemKey:guid}/{occurrence}/attendance/{membershipKey:guid}")]
    [ResourceAuthorize(ResourceType.KurinScore, ResourceAction.Read, "route:kurinKey")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public Task<IActionResult> UnmarkAttendance(Guid kurinKey, Guid agendaItemKey, string occurrence, Guid membershipKey) =>
        SendAtOccurrenceAsync(occurrence, startUtc => new UnmarkAttendanceCommand(kurinKey, agendaItemKey, startUtc, membershipKey));

    /// <summary>Says the occurrence did not take place: it leaves the провід's «Справи». Refused while anyone is marked present.</summary>
    [HttpPut("events/{agendaItemKey:guid}/{occurrence}/not-held")]
    [ResourceAuthorize(ResourceType.KurinScore, ResourceAction.Read, "route:kurinKey")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public Task<IActionResult> SetNotHeld(Guid kurinKey, Guid agendaItemKey, string occurrence) =>
        SendAtOccurrenceAsync(occurrence, startUtc => new SetOccurrenceNotHeldCommand(kurinKey, agendaItemKey, startUtc, NotHeld: true));

    /// <summary>Takes «did not take place» back; the first mark at the occurrence does it too.</summary>
    [HttpDelete("events/{agendaItemKey:guid}/{occurrence}/not-held")]
    [ResourceAuthorize(ResourceType.KurinScore, ResourceAction.Read, "route:kurinKey")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public Task<IActionResult> ClearNotHeld(Guid kurinKey, Guid agendaItemKey, string occurrence) =>
        SendAtOccurrenceAsync(occurrence, startUtc => new SetOccurrenceNotHeldCommand(kurinKey, agendaItemKey, startUtc, NotHeld: false));

    [HttpPost("entries")]
    [ResourceAuthorize(ResourceType.KurinScore, ResourceAction.Read, "route:kurinKey")]
    [ProducesResponseType(typeof(Guid), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateEntry(Guid kurinKey, [FromBody] UpsertScoreEntryRequest request)
    {
        var result = await _mediator.Send(new CreateScoreEntryCommand(kurinKey, request));
        return result.ToActionResult(this);
    }

    [HttpPut("entries/{entryKey:guid}")]
    [ResourceAuthorize(ResourceType.KurinScore, ResourceAction.Read, "route:kurinKey")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> UpdateEntry(Guid kurinKey, Guid entryKey, [FromBody] UpsertScoreEntryRequest request)
    {
        var result = await _mediator.Send(new UpdateScoreEntryCommand(kurinKey, entryKey, request));
        return result.ToActionResult(this);
    }

    [HttpDelete("entries/{entryKey:guid}")]
    [ResourceAuthorize(ResourceType.KurinScore, ResourceAction.Read, "route:kurinKey")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteEntry(Guid kurinKey, Guid entryKey)
    {
        var result = await _mediator.Send(new DeleteScoreEntryCommand(kurinKey, entryKey));
        return result.ToActionResult(this);
    }

    [HttpGet("settings")]
    [ResourceAuthorize(ResourceType.KurinScore, ResourceAction.Manage, "route:kurinKey")]
    [ProducesResponseType(typeof(KurinScoreSettingsDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSettings(Guid kurinKey)
    {
        var result = await _mediator.Send(new GetKurinScoreSettingsQuery(kurinKey));
        return result.ToActionResult(this);
    }

    [HttpPut("settings/algorithm")]
    [ResourceAuthorize(ResourceType.KurinScore, ResourceAction.Manage, "route:kurinKey")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> SetAlgorithm(Guid kurinKey, [FromBody] SetScoreAlgorithmRequest request)
    {
        var result = await _mediator.Send(new SetScoreAlgorithmCommand(kurinKey, request));
        return result.ToActionResult(this);
    }

    [HttpPut("settings/rules")]
    [ResourceAuthorize(ResourceType.KurinScore, ResourceAction.Manage, "route:kurinKey")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> SetRule(Guid kurinKey, [FromBody] SetScoreRuleRequest request)
    {
        var result = await _mediator.Send(new SetScoreRuleCommand(kurinKey, request));
        return result.ToActionResult(this);
    }

    [HttpPut("settings/attendance-rates")]
    [ResourceAuthorize(ResourceType.KurinScore, ResourceAction.Manage, "route:kurinKey")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SetAttendanceRate(Guid kurinKey, [FromBody] SetScoreAttendanceRateRequest request)
    {
        var result = await _mediator.Send(new SetScoreAttendanceRateCommand(kurinKey, request));
        return result.ToActionResult(this);
    }

    [HttpPost("settings/items")]
    [ResourceAuthorize(ResourceType.KurinScore, ResourceAction.Manage, "route:kurinKey")]
    [ProducesResponseType(typeof(Guid), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateItem(Guid kurinKey, [FromBody] UpsertScoreItemRequest request)
    {
        var result = await _mediator.Send(new UpsertScoreItemCommand(kurinKey, null, request));
        return result.ToActionResult(this);
    }

    [HttpPut("settings/items/{itemKey:guid}")]
    [ResourceAuthorize(ResourceType.KurinScore, ResourceAction.Manage, "route:kurinKey")]
    [ProducesResponseType(typeof(Guid), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateItem(Guid kurinKey, Guid itemKey, [FromBody] UpsertScoreItemRequest request)
    {
        var result = await _mediator.Send(new UpsertScoreItemCommand(kurinKey, itemKey, request));
        return result.ToActionResult(this);
    }

    [HttpPost("settings/stages")]
    [ResourceAuthorize(ResourceType.KurinScore, ResourceAction.Manage, "route:kurinKey")]
    [ProducesResponseType(typeof(Guid), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateStage(Guid kurinKey, [FromBody] UpsertScoreStageRequest request)
    {
        var result = await _mediator.Send(new UpsertScoreStageCommand(kurinKey, null, request));
        return result.ToActionResult(this);
    }

    [HttpPut("settings/stages/{stageKey:guid}")]
    [ResourceAuthorize(ResourceType.KurinScore, ResourceAction.Manage, "route:kurinKey")]
    [ProducesResponseType(typeof(Guid), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateStage(Guid kurinKey, Guid stageKey, [FromBody] UpsertScoreStageRequest request)
    {
        var result = await _mediator.Send(new UpsertScoreStageCommand(kurinKey, stageKey, request));
        return result.ToActionResult(this);
    }

    [HttpDelete("settings/stages/{stageKey:guid}")]
    [ResourceAuthorize(ResourceType.KurinScore, ResourceAction.Manage, "route:kurinKey")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteStage(Guid kurinKey, Guid stageKey)
    {
        var result = await _mediator.Send(new DeleteScoreStageCommand(kurinKey, stageKey));
        return result.ToActionResult(this);
    }

    /// <summary>
    /// Sends what the occurrence in the route asks for, or refuses an occurrence that is not a start
    /// instant with <c>BadOccurrence</c>.
    /// </summary>
    private async Task<IActionResult> SendAtOccurrenceAsync<T>(string occurrence, Func<DateTime, IRequest<ServiceResult<T>>> request)
    {
        if (!TryParseOccurrence(occurrence, out var startUtc))
        {
            return this.Failure(ResultType.BadRequest, ScoreErrors.BadOccurrence, "The occurrence is its start, as an ISO 8601 UTC instant.");
        }

        var result = await _mediator.Send(request(startUtc));
        return result.ToActionResult(this);
    }

    /// <summary>An occurrence is named by its start, "2026-10-06T16:00:00Z"; anything else is nothing.</summary>
    private static bool TryParseOccurrence(string occurrence, out DateTime startUtc) =>
        DateTime.TryParse(occurrence, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out startUtc);
}
