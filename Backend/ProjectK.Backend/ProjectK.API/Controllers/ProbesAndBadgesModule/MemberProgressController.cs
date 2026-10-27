using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ProjectK.API.Authorization;
using ProjectK.API.Extensions;
using ProjectK.API.Helpers;
using ProjectK.BusinessLogic.Modules.ProbesAndBadgesModule.Features.Badge.Get;
using ProjectK.BusinessLogic.Modules.ProbesAndBadgesModule.Features.Badge.Review;
using ProjectK.BusinessLogic.Modules.ProbesAndBadgesModule.Features.Badge.Submit;
using ProjectK.BusinessLogic.Modules.ProbesAndBadgesModule.Features.Probe.Get;
using ProjectK.BusinessLogic.Modules.ProbesAndBadgesModule.Features.Probe.UpdatePointSignature;
using ProjectK.BusinessLogic.Modules.ProbesAndBadgesModule.Features.Probe.UpdateStatus;
using ProjectK.BusinessLogic.Modules.ProbesAndBadgesModule.Models;
using ProjectK.Common.Extensions;
using ProjectK.Common.Models.Dtos.ProbesAndBadgesModule.Requests;
using ProjectK.Common.Models.Enums;

namespace ProjectK.API.Controllers.ProbesAndBadgesModule;

/// <summary>
/// One member's progress through probes and badges: what they have submitted, what has been signed off,
/// and by whom.
/// </summary>
[ApiController]
[Route("api/member/{memberKey:guid}")]
public class MemberProgressController : ControllerBase
{
    private readonly IMediator _mediator;

    public MemberProgressController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// Returns the member's progress on every badge they have started.
    /// </summary>
    [Authorize(Policy = AuthorizationPolicies.RequireUser)]
    [HttpGet("badges/progress")]
    [ResourceAuthorize(ResourceType.BadgeProgress, ResourceAction.Read, "route:memberKey", ResourceType.Member)]
    [ProducesResponseType(typeof(IEnumerable<BadgeProgressResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetBadgeProgresses(Guid memberKey)
    {
        var response = await _mediator.Send(new GetBadgeProgressesQuery(memberKey));
        return response.ToActionResult(this);
    }

    /// <summary>
    /// Submits a badge for review.
    /// </summary>
    /// <remarks>
    /// Members submit their own; submitting for somebody else needs rights over that member.
    /// </remarks>
    [Authorize(Policy = AuthorizationPolicies.RequireUser)]
    [HttpPost("badges/{badgeId}/submit")]
    [ResourceAuthorize(ResourceType.BadgeProgress, ResourceAction.Create, "route:memberKey", ResourceType.Member)]
    [ProducesResponseType(typeof(BadgeProgressResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> SubmitBadgeProgress(Guid memberKey, string badgeId, [FromBody] SubmitBadgeProgressRequest request)
    {
        var response = await _mediator.Send(new SubmitBadgeProgressCommand(memberKey, badgeId, request?.Note));
        return response.ToActionResult(this);
    }

    /// <summary>
    /// Accepts or refuses a submitted badge.
    /// </summary>
    [Authorize(Policy = AuthorizationPolicies.RequireUser)]
    [HttpPost("badges/{badgeId}/review")]
    [ResourceAuthorize(ResourceType.BadgeProgress, ResourceAction.Update, "route:memberKey", ResourceType.Member)]
    [ProducesResponseType(typeof(BadgeProgressResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ReviewBadgeProgress(Guid memberKey, string badgeId, [FromBody] ReviewBadgeProgressRequest request)
    {
        var response = await _mediator.Send(new ReviewBadgeProgressCommand(memberKey, badgeId, request.IsApproved, request.Note));
        return response.ToActionResult(this);
    }

    /// <summary>
    /// Returns the probes the member walks, in order.
    /// </summary>
    /// <remarks>
    /// The edition their гурток chose, unless a closed probe past the first holds them to another;
    /// a probe they started elsewhere keeps a place their edition lacks.
    /// </remarks>
    [Authorize(Policy = AuthorizationPolicies.RequireUser)]
    [HttpGet("probes/line")]
    [ResourceAuthorize(ResourceType.ProbeProgress, ResourceAction.Read, "route:memberKey", ResourceType.Member)]
    [ProducesResponseType(typeof(IReadOnlyList<ProbeSummaryResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetProbeLine(Guid memberKey)
    {
        var response = await _mediator.Send(new GetMemberProbeLineQuery(memberKey));
        return response.ToActionResult(this);
    }

    /// <summary>
    /// Returns the member's progress through one probe, point by point.
    /// </summary>
    [Authorize(Policy = AuthorizationPolicies.RequireUser)]
    [HttpGet("probes/{probeId}/progress")]
    [ResourceAuthorize(ResourceType.ProbeProgress, ResourceAction.Read, "route:memberKey", ResourceType.Member)]
    [ProducesResponseType(typeof(ProbeProgressResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetProbeProgress(Guid memberKey, string probeId)
    {
        var response = await _mediator.Send(new GetProbeProgressQuery(memberKey, probeId));
        return response.ToActionResult(this);
    }

    /// <summary>
    /// Moves a probe between statuses.
    /// </summary>
    [Authorize(Policy = AuthorizationPolicies.RequireUser)]
    [HttpPut("probes/{probeId}/progress/status")]
    [ResourceAuthorize(ResourceType.ProbeProgress, ResourceAction.Update, "route:memberKey", ResourceType.Member)]
    [ProducesResponseType(typeof(ProbeProgressResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> UpdateProbeProgressStatus(
        Guid memberKey,
        string probeId,
        [FromBody] UpdateProbeProgressStatusRequest request)
    {
        var response = await _mediator.Send(new UpdateProbeProgressStatusCommand(memberKey, probeId, request.Status, request.Note));
        return response.ToActionResult(this);
    }

    /// <summary>
    /// Signs off one point of a probe.
    /// </summary>
    /// <remarks>
    /// The signature records who signed and when, which is what the member's book is built from.
    /// </remarks>
    [Authorize(Policy = AuthorizationPolicies.RequireUser)]
    [HttpPut("probes/{probeId}/points/{pointId}/sign")]
    [ResourceAuthorize(ResourceType.ProbeProgress, ResourceAction.Update, "route:memberKey", ResourceType.Member)]
    [ProducesResponseType(typeof(ProbeProgressResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SignProbePoint(
        Guid memberKey,
        string probeId,
        string pointId,
        [FromBody] UpdateProbePointSignatureRequest? request)
    {
        var response = await _mediator.Send(new UpdateProbePointSignatureCommand(memberKey, probeId, pointId, true, request?.Note));
        return response.ToActionResult(this);
    }

    /// <summary>
    /// Withdraws a signature from one point of a probe.
    /// </summary>
    [Authorize(Policy = AuthorizationPolicies.RequireUser)]
    [HttpPut("probes/{probeId}/points/{pointId}/unsign")]
    [ResourceAuthorize(ResourceType.ProbeProgress, ResourceAction.Delete, "route:memberKey", ResourceType.Member)]
    [ProducesResponseType(typeof(ProbeProgressResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UnsignProbePoint(
        Guid memberKey,
        string probeId,
        string pointId,
        [FromBody] UpdateProbePointSignatureRequest? request)
    {
        var response = await _mediator.Send(new UpdateProbePointSignatureCommand(memberKey, probeId, pointId, false, request?.Note));
        return response.ToActionResult(this);
    }
}
