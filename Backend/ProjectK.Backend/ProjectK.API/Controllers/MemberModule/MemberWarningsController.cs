using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProjectK.API.Authorization;
using ProjectK.API.Extensions;
using ProjectK.API.Filters;
using ProjectK.BusinessLogic.Modules.MemberModule.Features.MemberWarning;
using ProjectK.Common.Dtos.MemberModule;
using ProjectK.Common.Dtos.MemberModule.Requests;
using ProjectK.Common.Enums.AuthModule;

namespace ProjectK.API.Controllers.MemberModule;

/// <summary>
/// Warnings recorded against a member. Assigning and cancelling are leadership decisions; the member
/// may read their own.
/// </summary>
[ApiController]
[Route("api/member/{memberKey:guid}/warnings")]
public class MemberWarningsController : ControllerBase
{
    private readonly IMediator _mediator;

    public MemberWarningsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// Lists the warnings recorded against a member.
    /// </summary>
    [Authorize(Policy = AuthorizationPolicies.RequireUser)]
    [HttpGet]
    [ResourceAuthorize(ResourceType.MemberWarning, ResourceAction.Read, "route:memberKey", ResourceType.Member)]
    [ProducesResponseType(typeof(IEnumerable<MemberWarningDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetWarnings(Guid memberKey)
    {
        var response = await _mediator.Send(new GetMemberWarningsQuery(memberKey));
        return response.ToActionResult(this);
    }

    /// <summary>
    /// Records a warning.
    /// </summary>
    [Authorize(Policy = AuthorizationPolicies.RequireUser)]
    [HttpPost]
    [ResourceAuthorize(ResourceType.MemberWarning, ResourceAction.Create, "route:memberKey", ResourceType.Member)]
    [ProducesResponseType(typeof(MemberWarningDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> AssignWarning(Guid memberKey, [FromBody] AssignMemberWarningRequest request)
    {
        var response = await _mediator.Send(new AssignMemberWarningCommand(memberKey, request.Level));
        return response.ToActionResult(this);
    }

    /// <summary>
    /// Cancels a warning.
    /// </summary>
    /// <remarks>
    /// Cancelling rather than deleting: the record stays, so the history remains readable. Who may
    /// cancel depends on the warning — its issuer while they could still give it, or whoever the
    /// kurin lets cancel anyone's — so the endpoint lets in whoever may see it and the handler decides.
    /// </remarks>
    [Authorize(Policy = AuthorizationPolicies.RequireUser)]
    [HttpDelete("{warningKey:guid}")]
    [ResourceAuthorize(ResourceType.MemberWarning, ResourceAction.Read, "route:memberKey", ResourceType.Member)]
    [ProducesResponseType(typeof(MemberWarningDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CancelWarning(Guid memberKey, Guid warningKey)
    {
        var response = await _mediator.Send(new CancelMemberWarningCommand(memberKey, warningKey));
        return response.ToActionResult(this);
    }
}
