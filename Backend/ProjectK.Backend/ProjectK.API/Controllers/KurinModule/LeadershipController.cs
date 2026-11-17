using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProjectK.API.Authorization;
using ProjectK.API.Extensions;
using ProjectK.API.Filters;
using ProjectK.BusinessLogic.Modules.KurinModule.Features.Leadership;
using ProjectK.Common.Dtos.KurinModule;
using ProjectK.Common.Dtos.KurinModule.Requests;
using ProjectK.Common.Dtos.MemberModule;
using ProjectK.Common.Enums.IdentityModule;

namespace ProjectK.API.Controllers.KurinModule;

/// <summary>
/// Offices — who holds which post in a kurin or a group. Seating somebody in an office is what grants
/// them their access, so these are the calls that change what a person may do.
/// </summary>
[Route("api/leadership")]
[ApiController]
public class LeadershipController : ControllerBase
{
    private readonly IMediator _mediator;

    public LeadershipController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// Returns the offices of one kurin or one group.
    /// </summary>
    /// <remarks>
    /// The resource being checked comes from the route, since the same endpoint serves both.
    /// </remarks>
    [Authorize(Policy = AuthorizationPolicies.RequireUser)]
    [HttpGet("type/{leadershipType}/{typeKey:guid}")]
    [ResourceAuthorize("route:leadershipType", ResourceAction.Read, "route:typeKey")]
    [ProducesResponseType(typeof(LeadershipDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetLeadershipByType(string leadershipType, Guid typeKey, CancellationToken cancellationToken)
    {
        var request = new GetLeadershipByTypeQuery(leadershipType, typeKey);
        var response = await _mediator.Send(request, cancellationToken);
        return response.ToActionResult(this);
    }

    /// <summary>
    /// Returns one office record.
    /// </summary>
    /// <remarks>
    /// Gated by the resource check alone rather than by a management tier. Курінний may seat the
    /// offices below him, so requiring whole-kurin management to read made reading stricter than
    /// writing — <see cref="UpdateLeadership"/> has always been open to him — and the edit page
    /// died on its first request.
    /// </remarks>
    [Authorize(Policy = AuthorizationPolicies.RequireUser)]
    [HttpGet("{leadershipKey:guid}")]
    [ResourceAuthorize(ResourceType.Leadership, ResourceAction.Read, "route:leadershipKey")]
    [ProducesResponseType(typeof(LeadershipDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetLeadershipByKey(Guid leadershipKey)
    {
        var request = new GetLeadershipByKeyQuery(leadershipKey);
        var response = await _mediator.Send(request);
        return response.ToActionResult(this);
    }

    /// <summary>
    /// Seats a member in an office.
    /// </summary>
    /// <remarks>
    /// The system role that decides access is derived from the office, so this call changes what that
    /// member may do everywhere.
    /// </remarks>
    [Authorize(Policy = AuthorizationPolicies.RequireUser)]
    [HttpPost]
    [ResourceAuthorize("arg:dto.Type", ResourceAction.Create, "arg:dto.EntityKey")]
    [ProducesResponseType(typeof(LeadershipDto), StatusCodes.Status201Created)]
    public async Task<IActionResult> CreateLeadership([FromBody] UpsertLeadershipRequest dto)
    {
        var request = new UpsertLeadershipCommand(dto);
        var response = await _mediator.Send(request);
        return response.ToActionResult(this);
    }

    /// <summary>
    /// Rewrites an office record, including who holds it.
    /// </summary>
    [Authorize(Policy = AuthorizationPolicies.RequireUser)]
    [HttpPut("{leadershipKey:guid}")]
    [ResourceAuthorize(ResourceType.Leadership, ResourceAction.Update, "route:leadershipKey")]
    [ProducesResponseType(typeof(LeadershipDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateLeadership(Guid leadershipKey, [FromBody] UpsertLeadershipRequest dto)
    {
        var request = new UpsertLeadershipCommand(dto, leadershipKey);
        var response = await _mediator.Send(request);
        return response.ToActionResult(this);
    }

    /// <summary>
    /// Returns who has held an office over time.
    /// </summary>
    [Authorize(Policy = AuthorizationPolicies.RequireUser)]
    [HttpGet("histories/{leadershipKey:guid}")]
    [ResourceAuthorize(ResourceType.Leadership, ResourceAction.Read, "route:leadershipKey")]
    [ProducesResponseType(typeof(IEnumerable<LeadershipHistoryMemberDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetLeadershipHistories(Guid leadershipKey)
    {
        var request = new GetLeadershipHistoriesQuery(leadershipKey);
        var response = await _mediator.Send(request);
        return response.ToActionResult(this);
    }

    /// <summary>
    /// Lists the members who sit in the kurin's governing body.
    /// </summary>
    [Authorize(Policy = AuthorizationPolicies.RequireUser)]
    [HttpGet("~/api/kurin/{kurinKey:guid}/kv")]
    // Old address, kept for PWAs still on the previous build; drop in 1.5.
    [HttpGet("~/api/member/members/kv/{kurinKey:guid}")]
    [ResourceAuthorize(ResourceType.Kurin, ResourceAction.Read, "route:kurinKey")]
    [ProducesResponseType(typeof(IEnumerable<MemberLookupDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetKurinKvMembers(Guid kurinKey)
    {
        var request = new GetKurinKvMembersQuery(kurinKey);
        var response = await _mediator.Send(request);
        return response.ToActionResult(this);
    }
}
