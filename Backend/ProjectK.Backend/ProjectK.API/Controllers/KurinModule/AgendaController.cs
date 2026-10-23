using System.Text.Json.Serialization;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ProjectK.API.Authorization;
using ProjectK.API.Extensions;
using ProjectK.API.Helpers;
using ProjectK.BusinessLogic.Modules.KurinModule.Features.Agenda.Archive;
using ProjectK.BusinessLogic.Modules.KurinModule.Features.Agenda.Categories;
using ProjectK.BusinessLogic.Modules.KurinModule.Features.Agenda.Create;
using ProjectK.BusinessLogic.Modules.KurinModule.Features.Agenda.Delete;
using ProjectK.BusinessLogic.Modules.KurinModule.Features.Agenda.Get;
using ProjectK.BusinessLogic.Modules.KurinModule.Features.Agenda.Responses;
using ProjectK.BusinessLogic.Modules.KurinModule.Features.Agenda.Status;
using ProjectK.BusinessLogic.Modules.KurinModule.Features.Agenda.Update;
using ProjectK.BusinessLogic.Modules.KurinModule.Models;
using ProjectK.Common.Extensions;
using ProjectK.Common.Models.Enums;

namespace ProjectK.API.Controllers.KurinModule;

/// <summary>
/// The kurin's agenda: items, the categories they are filed under, and who has answered that they are
/// coming. What a caller sees is decided by the assignments on each item, not by the item's author.
/// </summary>
[Route("api/[controller]")]
[ApiController]
public class AgendaController : ControllerBase
{
    private readonly IMediator _mediator;

    public AgendaController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// Returns agenda items in a date range, narrowed to what the caller may see.
    /// </summary>
    /// <remarks>
    /// Whole-kurin viewers see everything; everyone else sees only items assigned to them, their groups or
    /// their offices.
    /// </remarks>
    [Authorize(Policy = AuthorizationPolicies.RequireUser)]
    [HttpGet("{kurinKey:guid}")]
    [ResourceAuthorize(ResourceType.Kurin, ResourceAction.Read, "route:kurinKey")]
    [ProducesResponseType(typeof(IEnumerable<AgendaItemResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetCalendar(Guid kurinKey, [FromQuery] DateTime? fromUtc, [FromQuery] DateTime? toUtc, [FromQuery] bool includeSchedules = false)
    {
        var response = await _mediator.Send(new GetAgendaItemsQuery(kurinKey, fromUtc, toUtc, includeSchedules));
        return response.ToActionResult(this);
    }

    /// <summary>
    /// The live tasks as board columns, each paged on its own: with <paramref name="status"/> only that
    /// column's next page («Завантажити ще»), without it the first page of every column.
    /// </summary>
    [Authorize(Policy = AuthorizationPolicies.RequireUser)]
    [HttpGet("{kurinKey:guid}/board")]
    [ResourceAuthorize(ResourceType.Kurin, ResourceAction.Read, "route:kurinKey")]
    [ProducesResponseType(typeof(AgendaBoardResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetBoard(
        Guid kurinKey,
        [FromQuery] string? search,
        [FromQuery] AgendaTargetType? targetType,
        [FromQuery] Guid? targetKey,
        [FromQuery] bool onlyMine = false,
        [FromQuery] AgendaBoardSort sort = AgendaBoardSort.Due,
        [FromQuery] AgendaItemStatus? status = null,
        [FromQuery] int skip = 0,
        [FromQuery] int take = AgendaBoardPaging.DefaultTake)
    {
        var response = await _mediator.Send(new GetAgendaBoardQuery(kurinKey, new AgendaBoardFilter
        {
            Search = search,
            TargetType = targetType,
            TargetKey = targetKey,
            OnlyMine = onlyMine,
            Sort = sort,
            Status = status,
            Skip = skip,
            Take = take
        }));
        return response.ToActionResult(this);
    }

    /// <summary>Archived tasks, newest first, with the kurin's rules for how long they are kept.</summary>
    [Authorize(Policy = AuthorizationPolicies.RequireUser)]
    [HttpGet("{kurinKey:guid}/archive")]
    [ResourceAuthorize(ResourceType.Kurin, ResourceAction.Read, "route:kurinKey")]
    [ProducesResponseType(typeof(AgendaArchivePageResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetArchive(Guid kurinKey, [FromQuery] string? search, [FromQuery] int skip = 0, [FromQuery] int take = AgendaBoardPaging.DefaultTake)
    {
        var response = await _mediator.Send(new GetAgendaArchiveQuery(kurinKey, search, skip, take));
        return response.ToActionResult(this);
    }

    /// <summary>How long done tasks stay on the board and the archive is kept, for the kurin's settings.</summary>
    [Authorize(Policy = AuthorizationPolicies.RequireUser)]
    [HttpGet("{kurinKey:guid}/archive-policy")]
    [ResourceAuthorize(ResourceType.Kurin, ResourceAction.Update, "route:kurinKey")]
    [ProducesResponseType(typeof(AgendaArchivePolicyDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetArchivePolicy(Guid kurinKey)
    {
        var response = await _mediator.Send(new GetAgendaArchivePolicyQuery(kurinKey));
        return response.ToActionResult(this);
    }

    [Authorize(Policy = AuthorizationPolicies.RequireUser)]
    [HttpPut("{kurinKey:guid}/archive-policy")]
    [ResourceAuthorize(ResourceType.Kurin, ResourceAction.Update, "route:kurinKey")]
    [ProducesResponseType(typeof(AgendaArchivePolicyDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> SetArchivePolicy(Guid kurinKey, [FromBody] AgendaArchivePolicyDto request)
    {
        var response = await _mediator.Send(new SetAgendaArchivePolicyCommand(kurinKey, request.AutoArchiveAfterDays, request.PurgeAfterDays));
        return response.ToActionResult(this);
    }

    /// <summary>One item as the viewer sees it; not found when it is not theirs to see.</summary>
    [Authorize(Policy = AuthorizationPolicies.RequireUser)]
    [HttpGet("item/{agendaItemKey:guid}")]
    [ProducesResponseType(typeof(AgendaItemResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetItem(Guid agendaItemKey)
    {
        var response = await _mediator.Send(new GetAgendaItemQuery(agendaItemKey));
        return response.ToActionResult(this);
    }

    /// <summary>Moves a task into the archive, or back to the board.</summary>
    [Authorize(Policy = AuthorizationPolicies.RequireUser)]
    [HttpPut("{agendaItemKey:guid}/archive")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SetArchived(Guid agendaItemKey, [FromBody] SetAgendaArchivedRequest request)
    {
        var response = await _mediator.Send(new SetAgendaItemArchivedCommand(agendaItemKey, request.Archived));
        return response.ToActionResult(this);
    }

    /// <summary>
    /// Lists what an item can be assigned to — the kurin, its groups, its offices and its members —
    /// flagged by what the kurin's policy lets the caller assign; 403 when it lets them assign nothing.
    /// </summary>
    [Authorize(Policy = AuthorizationPolicies.RequireUser)]
    [HttpGet("{kurinKey:guid}/assign-targets")]
    [ResourceAuthorize(ResourceType.Kurin, ResourceAction.Read, "route:kurinKey")]
    [ProducesResponseType(typeof(AgendaAssignTargetsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetAssignTargets(Guid kurinKey)
    {
        var response = await _mediator.Send(new GetAssignTargetsQuery(kurinKey));
        return response.ToActionResult(this);
    }

    /// <summary>
    /// Creates an agenda item together with its assignments.
    /// </summary>
    [Authorize(Policy = AuthorizationPolicies.RequireUser)]
    [HttpPost]
    [ResourceAuthorize(ResourceType.Kurin, ResourceAction.Read, "arg:request.KurinKey")]
    [ProducesResponseType(typeof(Guid), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Create([FromBody] CreateAgendaItemCommand request)
    {
        var response = await _mediator.Send(request);
        return response.ToActionResult(this);
    }

    /// <summary>
    /// Rewrites an agenda item.
    /// </summary>
    [Authorize(Policy = AuthorizationPolicies.RequireUser)]
    [HttpPut("{agendaItemKey:guid}")]
    [ResourceAuthorize(ResourceType.AgendaItem, ResourceAction.Update, "route:agendaItemKey")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(Guid agendaItemKey, [FromBody] UpdateAgendaItemCommand request)
    {
        // The route key wins so a mismatched body cannot retarget another item.
        var response = await _mediator.Send(request with { AgendaItemKey = agendaItemKey });
        return response.ToActionResult(this);
    }

    /// <summary>
    /// Moves an item between statuses.
    /// </summary>
    /// <remarks>
    /// Separate from <see cref="Update"/> because who may change a status is not who may rewrite the item.
    /// </remarks>
    [Authorize(Policy = AuthorizationPolicies.RequireUser)]
    [HttpPut("{agendaItemKey:guid}/status")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ChangeStatus(Guid agendaItemKey, [FromBody] ChangeAgendaStatusRequest request)
    {
        var response = await _mediator.Send(new ChangeAgendaItemStatusCommand(agendaItemKey, request.Status));
        return response.ToActionResult(this);
    }

    /// <summary>
    /// Moves one target of a task, or one person's part of a target done «кожному окремо» — from the
    /// task's dialog, where each target and each person is named.
    /// </summary>
    [Authorize(Policy = AuthorizationPolicies.RequireUser)]
    [HttpPut("{agendaItemKey:guid}/assignments/{assignmentKey:guid}/status")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ChangeTargetStatus(Guid agendaItemKey, Guid assignmentKey, [FromBody] ChangeAgendaTargetStatusRequest request)
    {
        var response = await _mediator.Send(new ChangeAgendaTargetStatusCommand(agendaItemKey, assignmentKey, request.Status, request.MemberKey));
        return response.ToActionResult(this);
    }

    /// <summary>
    /// Deletes an agenda item.
    /// </summary>
    [Authorize(Policy = AuthorizationPolicies.RequireUser)]
    [HttpDelete("{agendaItemKey:guid}")]
    [ResourceAuthorize(ResourceType.AgendaItem, ResourceAction.Delete, "route:agendaItemKey")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid agendaItemKey)
    {
        var response = await _mediator.Send(new DeleteAgendaItemCommand(agendaItemKey));
        return response.ToActionResult(this);
    }

    // ---- Event groups (categories) ----

    /// <summary>
    /// Lists the categories available when filing an item.
    /// </summary>
    [Authorize(Policy = AuthorizationPolicies.RequireUser)]
    [HttpGet("{kurinKey:guid}/categories")]
    [ResourceAuthorize(ResourceType.Kurin, ResourceAction.Read, "route:kurinKey")]
    [ProducesResponseType(typeof(IEnumerable<AgendaCategoryResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetCategories(Guid kurinKey)
    {
        var response = await _mediator.Send(new GetAgendaCategoriesQuery(kurinKey, IncludeArchived: false));
        return response.ToActionResult(this);
    }

    /// <summary>
    /// Lists categories with the detail needed to edit them, which requires rights over the kurin.
    /// </summary>
    [Authorize(Policy = AuthorizationPolicies.RequireUser)]
    [HttpGet("{kurinKey:guid}/categories/manage")]
    [ResourceAuthorize(ResourceType.Kurin, ResourceAction.Update, "route:kurinKey")]
    [ProducesResponseType(typeof(IEnumerable<AgendaCategoryResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetCategoriesForManagement(Guid kurinKey)
    {
        var response = await _mediator.Send(new GetAgendaCategoriesQuery(kurinKey, IncludeArchived: true));
        return response.ToActionResult(this);
    }

    /// <summary>
    /// Creates a category, or rewrites one matched by key.
    /// </summary>
    [Authorize(Policy = AuthorizationPolicies.RequireUser)]
    [HttpPost("categories")]
    [ResourceAuthorize(ResourceType.Kurin, ResourceAction.Update, "arg:request.KurinKey")]
    [ProducesResponseType(typeof(AgendaCategoryResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> UpsertCategory([FromBody] UpsertAgendaCategoryCommand request)
    {
        var response = await _mediator.Send(request with { AgendaCategoryKey = null });
        return response.ToActionResult(this);
    }

    /// <summary>
    /// Rewrites one category.
    /// </summary>
    [Authorize(Policy = AuthorizationPolicies.RequireUser)]
    [HttpPut("categories/{categoryKey:guid}")]
    [ResourceAuthorize(ResourceType.Kurin, ResourceAction.Update, "arg:request.KurinKey")]
    [ProducesResponseType(typeof(AgendaCategoryResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateCategory(Guid categoryKey, [FromBody] UpsertAgendaCategoryCommand request)
    {
        // The route key wins so a mismatched body cannot retarget another group.
        var response = await _mediator.Send(request with { AgendaCategoryKey = categoryKey });
        return response.ToActionResult(this);
    }

    /// <summary>
    /// Deletes a category.
    /// </summary>
    [Authorize(Policy = AuthorizationPolicies.RequireUser)]
    [HttpDelete("{kurinKey:guid}/categories/{categoryKey:guid}")]
    [ResourceAuthorize(ResourceType.Kurin, ResourceAction.Update, "route:kurinKey")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteCategory(Guid kurinKey, Guid categoryKey)
    {
        var response = await _mediator.Send(new DeleteAgendaCategoryCommand(categoryKey, kurinKey));
        return response.ToActionResult(this);
    }

    // ---- RSVP ----

    /// <summary>
    /// Returns who has answered one occurrence of an item and how. <c>occurrenceStartUtc</c> names the
    /// occurrence of a series; a one-off event takes none.
    /// </summary>
    [Authorize(Policy = AuthorizationPolicies.RequireUser)]
    [HttpGet("{agendaItemKey:guid}/responses")]
    [ProducesResponseType(typeof(AgendaResponsesResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetResponses(Guid agendaItemKey, [FromQuery] DateTime? occurrenceStartUtc = null)
    {
        var response = await _mediator.Send(new GetAgendaResponsesQuery(agendaItemKey, occurrenceStartUtc));
        return response.ToActionResult(this);
    }

    /// <summary>
    /// Records the caller's own answer to one occurrence of an item.
    /// </summary>
    /// <remarks>
    /// Refused on items the caller cannot see, using the same visibility rule as the feed — the two are one
    /// definition, so the list and what may be answered cannot drift apart.
    /// </remarks>
    [Authorize(Policy = AuthorizationPolicies.RequireUser)]
    [HttpPut("{agendaItemKey:guid}/response")]
    [ProducesResponseType(typeof(AgendaResponsesResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SetResponse(Guid agendaItemKey, [FromBody] SetAgendaResponseRequest request)
    {
        var response = await _mediator.Send(new SetAgendaResponseCommand(agendaItemKey, request.OccurrenceStartUtc, request.Status));
        return response.ToActionResult(this);
    }
}

/// <summary>Body for the board status move.</summary>
public sealed record ChangeAgendaStatusRequest([property: JsonRequired] AgendaItemStatus Status);

/// <summary>Body for archiving a task (true) or taking it back (false).</summary>
public sealed record SetAgendaArchivedRequest([property: JsonRequired] bool Archived);

/// <summary>Body for moving one target; <c>MemberKey</c> names whose part, in «кожному окремо».</summary>
public sealed record ChangeAgendaTargetStatusRequest([property: JsonRequired] AgendaItemStatus Status, Guid? MemberKey);

/// <summary>Body for an RSVP set; <c>OccurrenceStartUtc</c> names the occurrence of a series, null for a one-off event.</summary>
public sealed record SetAgendaResponseRequest([property: JsonRequired] AgendaRsvpStatus Status, DateTime? OccurrenceStartUtc = null);
