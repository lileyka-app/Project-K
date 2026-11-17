using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProjectK.API.Authorization;
using ProjectK.API.Extensions;
using ProjectK.API.Filters;
using ProjectK.BusinessLogic.Modules.KurinModule.Features.PlanningSession;
using ProjectK.Common.Dtos.KurinModule;
using ProjectK.Common.Enums.IdentityModule;

namespace ProjectK.API.Controllers.KurinModule;

/// <summary>
/// Planning sessions — the working documents behind what later becomes the agenda.
/// </summary>
[Route("api/[controller]")]
[ApiController]
public class PlanningController : ControllerBase
{
    private readonly IMediator _mediator;
    public PlanningController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// Creates a planning session.
    /// </summary>
    [Authorize(Policy = AuthorizationPolicies.RequirePlanningAuthor)]
    [HttpPost]
    [ResourceAuthorize(ResourceType.Kurin, ResourceAction.Read, "arg:request.KurinKey")]
    [ProducesResponseType(typeof(Guid), StatusCodes.Status200OK)]
    public async Task<IActionResult> CreatePlanningSession([FromBody] CreatePlanningSessionCommand request)
    {
        var response = await _mediator.Send(request);
        return response.ToActionResult(this);
    }

    /// <summary>
    /// Returns one planning session.
    /// </summary>
    /// <remarks>
    /// Readable by anyone in the kurin. Planning is the kurin's own working record, and every member
    /// already carries <c>PlanningSession:Read:KurinWide</c> — a leadership gate on top only meant
    /// the map promised a read the endpoint refused.
    /// </remarks>
    [Authorize(Policy = AuthorizationPolicies.RequireUser)]
    [HttpGet("session/{planningSessionKey:guid}")]
    [ResourceAuthorize(ResourceType.PlanningSession, ResourceAction.Read, "route:planningSessionKey")]
    [ProducesResponseType(typeof(PlanningSessionDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPlanningSessionByKey(Guid planningSessionKey)
    {
        var request = new GetPlanningSessionByKeyQuery(planningSessionKey);
        var response = await _mediator.Send(request);
        return response.ToActionResult(this);
    }

    /// <summary>
    /// Lists the planning sessions of one kurin.
    /// </summary>
    /// <remarks>Readable by anyone in the kurin, like the single session below it.</remarks>
    [Authorize(Policy = AuthorizationPolicies.RequireUser)]
    [HttpGet("{kurinKey:guid}")]
    [ResourceAuthorize(ResourceType.Kurin, ResourceAction.Read, "route:kurinKey")]
    [ProducesResponseType(typeof(IEnumerable<PlanningSessionDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPlanningSessions(Guid kurinKey)
    {
        var request = new GetPlanningSessionsQuery(kurinKey);
        var response = await _mediator.Send(request);
        return response.ToActionResult(this);
    }

    /// <summary>
    /// Deletes a planning session.
    /// </summary>
    /// <remarks>
    /// Its author, or whole-kurin management. The провід holds
    /// <c>PlanningSession:Delete:Own</c> — <c>Own</c> here means the account that opened the session
    /// — so requiring whole-kurin management left an author unable to withdraw their own draft.
    /// </remarks>
    [Authorize(Policy = AuthorizationPolicies.RequireUser)]
    [HttpDelete("{planningSessionKey:guid}")]
    [ResourceAuthorize(ResourceType.PlanningSession, ResourceAction.Delete, "route:planningSessionKey")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> DeletePlanningSession(Guid planningSessionKey)
    {
        var request = new DeletePlanningSessionCommand(planningSessionKey);
        var response = await _mediator.Send(request);
        return response.ToActionResult(this);
    }
}
