using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ProjectK.API.Authorization;
using ProjectK.API.Extensions;
using ProjectK.API.Filters;
using ProjectK.BusinessLogic.Modules.KurinModule.Features.MembershipRequest;
using ProjectK.BusinessLogic.Modules.KurinModule.Features.MembershipRequest;
using ProjectK.BusinessLogic.Modules.KurinModule.Features.MembershipRequest;
using ProjectK.BusinessLogic.Modules.KurinModule.Features.MembershipRequest;
using ProjectK.BusinessLogic.Modules.KurinModule.Features.MembershipRequest;
using ProjectK.BusinessLogic.Modules.KurinModule.Features.MembershipRequest;
using ProjectK.BusinessLogic.Modules.KurinModule.Models;
using ProjectK.Common.Extensions;
using ProjectK.Common.Models.Dtos.KurinModule.Requests;
using ProjectK.Common.Models.Enums;

namespace ProjectK.API.Controllers.KurinModule;

/// <summary>
/// Requests between kurins to transfer a person or to let them stand in both: those this kurin
/// answers and those it sent.
/// </summary>
[Route("api/kurin/{kurinKey:guid}/membership-requests")]
[ApiController]
public class KurinMembershipRequestsController : ControllerBase
{
    private readonly IMediator _mediator;

    public KurinMembershipRequestsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// This kurin's requests both ways: those its Звʼязковий answers and those it sent.
    /// </summary>
    [Authorize(Policy = AuthorizationPolicies.RequireKurinManagement)]
    [HttpGet]
    [ResourceAuthorize(ResourceType.Kurin, ResourceAction.Update, "route:kurinKey")]
    [ProducesResponseType(typeof(MembershipRequestsResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> MembershipRequests(Guid kurinKey)
    {
        var response = await _mediator.Send(new GetMembershipRequestsQuery(kurinKey));
        return response.ToActionResult(this);
    }

    /// <summary>How many requests from other kurins wait for this kurin's answer: the menu's dot.</summary>
    [Authorize(Policy = AuthorizationPolicies.RequireKurinManagement)]
    [HttpGet("waiting")]
    [ResourceAuthorize(ResourceType.Kurin, ResourceAction.Update, "route:kurinKey")]
    [ProducesResponseType(typeof(int), StatusCodes.Status200OK)]
    public async Task<IActionResult> WaitingMembershipRequests(Guid kurinKey)
    {
        var response = await _mediator.Send(new CountWaitingMembershipRequestsQuery(kurinKey));
        return response.ToActionResult(this);
    }

    /// <summary>
    /// Former members with the kurins they stand in now, for the Звʼязковий deciding how to take someone back.
    /// </summary>
    [Authorize(Policy = AuthorizationPolicies.RequireKurinManagement)]
    [HttpGet("former")]
    [ResourceAuthorize(ResourceType.Kurin, ResourceAction.Update, "route:kurinKey")]
    [ProducesResponseType(typeof(IReadOnlyList<FormerMemberStanding>), StatusCodes.Status200OK)]
    public async Task<IActionResult> FormerStanding(Guid kurinKey)
    {
        var response = await _mediator.Send(new GetFormerStandingQuery(kurinKey));
        return response.ToActionResult(this);
    }

    /// <summary>
    /// Asks the kurin a person stands in to let them go («перевести») or to let them stand here too
    /// («прийняти ще й до нас»). Someone who stands in no kurin is taken in at once.
    /// </summary>
    [Authorize(Policy = AuthorizationPolicies.RequireKurinManagement)]
    [HttpPost]
    [ResourceAuthorize(ResourceType.Kurin, ResourceAction.Update, "route:kurinKey")]
    [ProducesResponseType(typeof(MembershipRequestSent), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateMembershipRequest(Guid kurinKey, [FromBody] CreateMembershipRequestRequest request)
    {
        var response = await _mediator.Send(new CreateMembershipRequestCommand(
            kurinKey, request.PublicId, request.MemberKey, request.Kind, request.MembershipKind, request.FromKurinKey));
        return response.ToActionResult(this);
    }

    /// <summary>The person's kurin approves a request. «Перевести» waits until the person owes it no dues.</summary>
    [Authorize(Policy = AuthorizationPolicies.RequireKurinManagement)]
    [HttpPost("{requestKey:guid}/approve")]
    [ResourceAuthorize(ResourceType.Kurin, ResourceAction.Update, "route:kurinKey")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ApproveMembershipRequest(Guid kurinKey, Guid requestKey)
    {
        var response = await _mediator.Send(new DecideMembershipRequestCommand(kurinKey, requestKey, Approve: true));
        return response.ToActionResult(this);
    }

    /// <summary>The person's kurin declines a request.</summary>
    [Authorize(Policy = AuthorizationPolicies.RequireKurinManagement)]
    [HttpPost("{requestKey:guid}/decline")]
    [ResourceAuthorize(ResourceType.Kurin, ResourceAction.Update, "route:kurinKey")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> DeclineMembershipRequest(Guid kurinKey, Guid requestKey)
    {
        var response = await _mediator.Send(new DecideMembershipRequestCommand(kurinKey, requestKey, Approve: false));
        return response.ToActionResult(this);
    }

    /// <summary>The asking kurin withdraws a request nobody has answered.</summary>
    [Authorize(Policy = AuthorizationPolicies.RequireKurinManagement)]
    [HttpDelete("{requestKey:guid}")]
    [ResourceAuthorize(ResourceType.Kurin, ResourceAction.Update, "route:kurinKey")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CancelMembershipRequest(Guid kurinKey, Guid requestKey)
    {
        var response = await _mediator.Send(new CancelMembershipRequestCommand(kurinKey, requestKey));
        return response.ToActionResult(this);
    }
}
