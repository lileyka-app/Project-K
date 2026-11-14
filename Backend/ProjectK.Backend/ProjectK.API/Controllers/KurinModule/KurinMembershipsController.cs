using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ProjectK.API.Authorization;
using ProjectK.API.Extensions;
using ProjectK.API.Filters;
using ProjectK.BusinessLogic.Modules.KurinModule.Features.Membership;
using ProjectK.BusinessLogic.Modules.KurinModule.Features.Membership;
using ProjectK.BusinessLogic.Modules.KurinModule.Features.Membership;
using ProjectK.BusinessLogic.Modules.KurinModule.Features.Membership;
using ProjectK.BusinessLogic.Modules.KurinModule.Features.Membership;
using ProjectK.Common.Extensions;
using ProjectK.Common.Models.Dtos.KurinModule.Requests;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Records;

namespace ProjectK.API.Controllers.KurinModule;

/// <summary>
/// Who stands in the kurin: taking a person in, letting them go, and placing them in a гурток.
/// </summary>
[Route("api/kurin/{kurinKey:guid}/memberships")]
[ApiController]
public class KurinMembershipsController : ControllerBase
{
    private readonly IMediator _mediator;

    public KurinMembershipsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// Takes a person into this kurin. They keep every other kurin they belong to and everything
    /// they have earned anywhere — this adds a membership, it does not move them.
    /// </summary>
    [Authorize(Policy = AuthorizationPolicies.RequireUser)]
    [HttpPost]
    [ResourceAuthorize(ResourceType.Member, ResourceAction.Create, "route:kurinKey", ResourceType.Kurin)]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Join(Guid kurinKey, [FromBody] JoinKurinRequest request)
    {
        var response = await _mediator.Send(
            new JoinKurinCommand(request.MemberKey, kurinKey, request.GroupKey, request.Kind, request.PublicId, OnlyIfStandingNowhere: true));
        return response.ToActionResult(this);
    }

    /// <summary>
    /// Looks up the person a public code names, so the провід can be sure before taking them in.
    /// </summary>
    /// <remarks>
    /// An exact match on a code someone handed over — not a search. The card carries no kurins
    /// and no contact details: answering "where else is this person" to anyone holding a code
    /// would tell more about them than they agreed to.
    /// </remarks>
    [Authorize(Policy = AuthorizationPolicies.RequireUser)]
    [HttpGet("candidate")]
    [ResourceAuthorize(ResourceType.Member, ResourceAction.Create, "route:kurinKey", ResourceType.Kurin)]
    [ProducesResponseType(typeof(MembershipCandidate), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> FindCandidate(Guid kurinKey, [FromQuery] string publicId)
    {
        var response = await _mediator.Send(new FindMemberByPublicIdQuery(kurinKey, publicId));
        return response.ToActionResult(this);
    }

    /// <summary>
    /// Who used to belong to this kurin, so the провід can see whom it let go — and take any of
    /// them back with <c>POST {kurinKey}/memberships</c>, without needing the code they hold.
    /// </summary>
    /// <remarks>
    /// Read against this kurin's own memberships. It answers "who was here and when did they
    /// leave" and nothing else: not where they are now, not how to reach them.
    /// </remarks>
    [Authorize(Policy = AuthorizationPolicies.RequireUser)]
    [HttpGet("former")]
    [ResourceAuthorize(ResourceType.Member, ResourceAction.Create, "route:kurinKey", ResourceType.Kurin)]
    [ProducesResponseType(typeof(IReadOnlyCollection<FormerMember>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> FormerMembers(Guid kurinKey)
    {
        var response = await _mediator.Send(new GetFormerMembersQuery(kurinKey));
        return response.ToActionResult(this);
    }

    /// <summary>
    /// Ends a person's membership in this kurin. Their record and their history stay where they
    /// are; only the belonging is closed.
    /// </summary>
    /// <remarks>
    /// Only the Звʼязковий closes a membership: a youth does not leave on their own, and a
    /// Виховник does not release someone from the kurin.
    /// </remarks>
    [Authorize(Policy = AuthorizationPolicies.RequireKurinManagement)]
    [HttpDelete("{memberKey:guid}")]
    [ResourceAuthorize(ResourceType.Member, ResourceAction.Delete, "route:memberKey")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Leave(Guid kurinKey, Guid memberKey)
    {
        var response = await _mediator.Send(new LeaveKurinCommand(memberKey, kurinKey));
        return response.ToActionResult(this);
    }

    /// <summary>
    /// Moves a person between гуртки of this kurin, or out of one — a null гурток is allowed.
    /// </summary>
    /// <remarks>
    /// Placement is the Звʼязковий's, like closing a membership. <c>Member:Update</c> is not enough:
    /// everyone holds it on their own record and a Виховник on his гурток, so either could move
    /// themselves, or someone else, into a гурток they chose.
    /// </remarks>
    [Authorize(Policy = AuthorizationPolicies.RequireKurinManagement)]
    [HttpPut("{memberKey:guid}/group")]
    [ResourceAuthorize(ResourceType.Member, ResourceAction.Manage, "route:memberKey")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> MoveToGroup(Guid kurinKey, Guid memberKey, [FromBody] MoveToGroupRequest request)
    {
        var response = await _mediator.Send(new MoveToGroupCommand(memberKey, kurinKey, request.GroupKey));
        return response.ToActionResult(this);
    }
}
