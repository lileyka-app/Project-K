using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ProjectK.API.Authorization;
using ProjectK.API.Extensions;
using ProjectK.API.Filters;
using ProjectK.BusinessLogic.Modules.KurinModule.Features.BranchRequest;
using ProjectK.BusinessLogic.Modules.KurinModule.Features.BranchRequest;
using ProjectK.BusinessLogic.Modules.KurinModule.Features.BranchRequest;
using ProjectK.BusinessLogic.Modules.KurinModule.Models;
using ProjectK.Common.Extensions;
using ProjectK.Common.Models.Dtos.KurinModule.Requests;
using ProjectK.Common.Models.Enums;

namespace ProjectK.API.Controllers.KurinModule;

/// <summary>
/// A senior kurin asking the administrators for its гілки and numbers, and the administrators'
/// answer.
/// </summary>
[Route("api/kurin")]
[ApiController]
public class KurinBranchRequestsController : ControllerBase
{
    private readonly IMediator _mediator;

    public KurinBranchRequestsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// The request a senior kurin is waiting on to change its гілки, or nothing. Read by its settings.
    /// </summary>
    [Authorize(Policy = AuthorizationPolicies.RequireUser)]
    [HttpGet("{kurinKey:guid}/branch-request")]
    [ResourceAuthorize(ResourceType.KurinSettings, ResourceAction.Read, "route:kurinKey")]
    [ProducesResponseType(typeof(KurinBranchRequestResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetBranchRequest(Guid kurinKey, CancellationToken cancellationToken)
    {
        var response = await _mediator.Send(new GetKurinBranchRequestQuery(kurinKey), cancellationToken);
        return response.ToActionResult(this);
    }

    /// <summary>
    /// A senior kurin asks the administrators for these гілки and numbers; a number in a гілка of the
    /// край is theirs to hand out. Replaces a request still waiting.
    /// </summary>
    [Authorize(Policy = AuthorizationPolicies.RequireUser)]
    [HttpPost("{kurinKey:guid}/branch-request")]
    [ResourceAuthorize(ResourceType.Kurin, ResourceAction.Update, "route:kurinKey")]
    [ProducesResponseType(typeof(KurinBranchRequestResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> SubmitBranchRequest(Guid kurinKey, [FromBody] SubmitKurinBranchRequest body, CancellationToken cancellationToken)
    {
        var response = await _mediator.Send(new SubmitKurinBranchRequestCommand(kurinKey, body.UspNumber, body.UpsNumber, body.Note), cancellationToken);
        return response.ToActionResult(this);
    }

    /// <summary>Every kurin's request still waiting, for the administrators' list beside the applications.</summary>
    [Authorize(Policy = AuthorizationPolicies.RequireAdmin)]
    [HttpGet("branch-requests")]
    [ProducesResponseType(typeof(IReadOnlyList<KurinBranchRequestResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPendingBranchRequests(CancellationToken cancellationToken)
    {
        var response = await _mediator.Send(new GetPendingKurinBranchRequestsQuery(), cancellationToken);
        return response.ToActionResult(this);
    }

    /// <summary>Gives the kurin the гілки it asked for. Refused while a number is taken in its гілка.</summary>
    [Authorize(Policy = AuthorizationPolicies.RequireAdmin)]
    [HttpPost("branch-requests/{requestKey:guid}/approve")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ApproveBranchRequest(Guid requestKey, CancellationToken cancellationToken)
    {
        var response = await _mediator.Send(new ReviewKurinBranchRequestCommand(requestKey, Approve: true), cancellationToken);
        return response.ToActionResult(this);
    }

    /// <summary>Turns the request down; the kurin keeps its гілки and hears of it.</summary>
    [Authorize(Policy = AuthorizationPolicies.RequireAdmin)]
    [HttpPost("branch-requests/{requestKey:guid}/reject")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RejectBranchRequest(Guid requestKey, CancellationToken cancellationToken)
    {
        var response = await _mediator.Send(new ReviewKurinBranchRequestCommand(requestKey, Approve: false), cancellationToken);
        return response.ToActionResult(this);
    }
}
