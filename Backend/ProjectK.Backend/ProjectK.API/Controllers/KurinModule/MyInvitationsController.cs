using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProjectK.API.Authorization;
using ProjectK.API.Extensions;
using ProjectK.BusinessLogic.Modules.KurinModule.Features.MembershipRequest;
using ProjectK.Common.Dtos.KurinModule;
using ProjectK.Common.Dtos.KurinModule.Requests;

namespace ProjectK.API.Controllers.KurinModule;

/// <summary>Kurins waiting on the caller's own answer to their invitation. Being signed in is the whole check.</summary>
[ApiController]
[Route("api/me")]
[Authorize(Policy = AuthorizationPolicies.RequireUser)]
public class MyInvitationsController : ControllerBase
{
    private readonly IMediator _mediator;

    public MyInvitationsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>Kurins waiting on the person's own answer to their invitation.</summary>
    [HttpGet("invitations")]
    [ProducesResponseType(typeof(IReadOnlyList<MembershipInvitationDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetInvitations()
    {
        var result = await _mediator.Send(new GetMyInvitationsQuery());
        return result.ToActionResult(this);
    }

    /// <summary>Accepts or declines one invitation; accepting adds a membership and closes nothing.</summary>
    [HttpPost("invitations/{membershipRequestKey:guid}/answer")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> AnswerInvitation(Guid membershipRequestKey, [FromBody] AnswerInvitationRequest request)
    {
        var result = await _mediator.Send(new AnswerMyInvitationCommand(membershipRequestKey, request.Accept));
        return result.ToActionResult(this);
    }
}
