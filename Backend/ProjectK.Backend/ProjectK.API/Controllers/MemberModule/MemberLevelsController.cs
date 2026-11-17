using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProjectK.API.Authorization;
using ProjectK.API.Extensions;
using ProjectK.API.Filters;
using ProjectK.BusinessLogic.Modules.MemberModule.Features.Level;
using ProjectK.Common.Dtos.MemberModule;
using ProjectK.Common.Enums.IdentityModule;

namespace ProjectK.API.Controllers.MemberModule;

/// <summary>
/// The ступені on a member's card that wait for the Звʼязковий: a senior one the person entered for
/// themselves counts only once confirmed.
/// </summary>
[ApiController]
[Route("api/member/{memberKey:guid}/levels")]
// Says what it means: authenticated. The scope check is the ResourceAuthorize filter on each action.
[Authorize(Policy = AuthorizationPolicies.RequireUser)]
public class MemberLevelsController : ControllerBase
{
    private readonly IMediator _mediator;

    public MemberLevelsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// Confirms a waiting ступінь, so it counts for the person's улад and highest ступінь.
    /// </summary>
    /// <remarks>
    /// The resource check admits whoever may update this member at all; that it takes the kurin-wide
    /// right where the person stands, and never the person themselves, is the handler's to check.
    /// </remarks>
    [HttpPost("{levelKey:guid}/confirm")]
    [ResourceAuthorize(ResourceType.Member, ResourceAction.Update, "route:memberKey")]
    [ProducesResponseType(typeof(PlastLevelHistoryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Confirm(Guid memberKey, Guid levelKey, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new ConfirmMemberLevelCommand(memberKey, levelKey), cancellationToken);
        return result.ToActionResult(this);
    }

    /// <summary>
    /// Rejects a waiting ступінь; it is removed from the card.
    /// </summary>
    /// <remarks>The same right as <see cref="Confirm"/>.</remarks>
    [HttpPost("{levelKey:guid}/reject")]
    [ResourceAuthorize(ResourceType.Member, ResourceAction.Update, "route:memberKey")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Reject(Guid memberKey, Guid levelKey, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new RejectMemberLevelCommand(memberKey, levelKey), cancellationToken);
        return result.ToActionResult(this);
    }
}
