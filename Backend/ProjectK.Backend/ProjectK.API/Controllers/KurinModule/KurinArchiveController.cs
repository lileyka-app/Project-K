using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using ProjectK.API.Authorization;
using ProjectK.API.Extensions;
using ProjectK.API.Helpers;
using ProjectK.BusinessLogic.Modules.KurinModule.Features.Archive.Correct;
using ProjectK.BusinessLogic.Modules.KurinModule.Features.Archive.DangerZone;
using ProjectK.BusinessLogic.Modules.KurinModule.Features.Archive.Delete;
using ProjectK.BusinessLogic.Modules.KurinModule.Features.Archive.Get;
using ProjectK.Common.Models.Dtos.KurinModule;
using ProjectK.Common.Models.Dtos.KurinModule.Requests;

namespace ProjectK.API.Controllers.KurinModule;

/// <summary>
/// The kurin's archive: former offices, memberships and dissolved гуртки. The Звʼязковий reads and
/// corrects all of it, a гурток's провід its own гуртки; nobody their own record. Deleting is the
/// danger zone: the Звʼязковий proves themselves again and carries the ticket in <c>X-Archive-Danger</c>.
/// </summary>
/// <remarks>
/// Who reaches what is decided in the handlers, from <c>KurinArchive</c> permissions and the гуртки
/// the caller leads, as for the journal: a group-scoped grant has no single resource to check here.
/// </remarks>
[ApiController]
[Route("api/kurin/{kurinKey:guid}/archive")]
public class KurinArchiveController : ControllerBase
{
    public const string DangerTicketHeader = "X-Archive-Danger";

    private readonly IMediator _mediator;

    public KurinArchiveController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>The archive as far as the caller reaches it.</summary>
    [Authorize(Policy = AuthorizationPolicies.RequireUser)]
    [HttpGet]
    [ProducesResponseType(typeof(KurinArchiveDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Get(Guid kurinKey, CancellationToken cancellationToken)
        => (await _mediator.Send(new GetKurinArchiveQuery(kurinKey), cancellationToken)).ToActionResult(this);

    /// <summary>Corrects a former holding's role or dates.</summary>
    [Authorize(Policy = AuthorizationPolicies.RequireUser)]
    [HttpPut("offices/{historyKey:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CorrectOffice(Guid kurinKey, Guid historyKey, [FromBody] CorrectArchivedOfficeRequest request, CancellationToken cancellationToken)
        => (await _mediator.Send(new CorrectArchivedOfficeCommand(kurinKey, historyKey, request.Role, request.StartDate, request.EndDate), cancellationToken)).ToActionResult(this);

    /// <summary>Corrects a former membership's kind or dates.</summary>
    [Authorize(Policy = AuthorizationPolicies.RequireUser)]
    [HttpPut("memberships/{membershipKey:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CorrectMembership(Guid kurinKey, Guid membershipKey, [FromBody] CorrectArchivedMembershipRequest request, CancellationToken cancellationToken)
        => (await _mediator.Send(new CorrectArchivedMembershipCommand(kurinKey, membershipKey, request.Kind, request.JoinedOn, request.LeftOn), cancellationToken)).ToActionResult(this);

    /// <summary>Turns the danger zone on: ten minutes, ten deletions.</summary>
    /// <remarks>Only the Звʼязковий. A wrong password or code answers 400, not 401: the session itself is fine.</remarks>
    [Authorize(Policy = AuthorizationPolicies.RequireUser)]
    [HttpPost("danger")]
    [EnableRateLimiting("StrictAuthLimit")]
    [ProducesResponseType(typeof(ArchiveDangerZoneDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> EnterDangerZone(Guid kurinKey, [FromBody] EnterArchiveDangerZoneRequest request, CancellationToken cancellationToken)
        => (await _mediator.Send(new EnterArchiveDangerZoneCommand(kurinKey, request.Password, request.Code), cancellationToken)).ToActionResult(this);

    /// <summary>Turns the caller's danger zone off.</summary>
    [Authorize(Policy = AuthorizationPolicies.RequireUser)]
    [HttpDelete("danger")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> EndDangerZone(Guid kurinKey, [FromQuery] ArchiveDangerZoneEnd reason = ArchiveDangerZoneEnd.Manual, CancellationToken cancellationToken = default)
        => (await _mediator.Send(new EndArchiveDangerZoneCommand(kurinKey, reason), cancellationToken)).ToActionResult(this);

    /// <summary>Deletes a former holding for good.</summary>
    [Authorize(Policy = AuthorizationPolicies.RequireUser)]
    [HttpDelete("danger/offices/{historyKey:guid}")]
    [ProducesResponseType(typeof(ArchiveDeletionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> DeleteOffice(Guid kurinKey, Guid historyKey, [FromHeader(Name = DangerTicketHeader)] string? ticket, CancellationToken cancellationToken)
        => (await _mediator.Send(new DeleteArchivedOfficeCommand(kurinKey, historyKey, ticket), cancellationToken)).ToActionResult(this);

    /// <summary>Deletes a former membership for good, with the offices held in it.</summary>
    [Authorize(Policy = AuthorizationPolicies.RequireUser)]
    [HttpDelete("danger/memberships/{membershipKey:guid}")]
    [ProducesResponseType(typeof(ArchiveDeletionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> DeleteMembership(Guid kurinKey, Guid membershipKey, [FromHeader(Name = DangerTicketHeader)] string? ticket, CancellationToken cancellationToken)
        => (await _mediator.Send(new DeleteArchivedMembershipCommand(kurinKey, membershipKey, ticket), cancellationToken)).ToActionResult(this);

    /// <summary>Deletes a dissolved гурток for good, with its провід's history.</summary>
    [Authorize(Policy = AuthorizationPolicies.RequireUser)]
    [HttpDelete("danger/groups/{groupKey:guid}")]
    [ProducesResponseType(typeof(ArchiveDeletionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> DeleteGroup(Guid kurinKey, Guid groupKey, [FromHeader(Name = DangerTicketHeader)] string? ticket, CancellationToken cancellationToken)
        => (await _mediator.Send(new DeleteDissolvedGroupCommand(kurinKey, groupKey, ticket), cancellationToken)).ToActionResult(this);
}
