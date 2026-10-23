using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProjectK.API.Authorization;
using ProjectK.API.Extensions;
using ProjectK.API.Helpers;
using ProjectK.BusinessLogic.Modules.AuditModule.Features.Journal.Get;
using ProjectK.BusinessLogic.Modules.AuditModule.Models;
using ProjectK.Common.Models.Enums;

namespace ProjectK.API.Controllers.AuditModule;

/// <summary>
/// «Журнал дій» of a kurin: who did what, to whom, and what changed. How much of it a caller sees
/// is the kurin's policy — the whole kurin, the гуртки they lead, or nothing.
/// </summary>
[ApiController]
[Route("api/kurin/{kurinKey:guid}/journal")]
public class KurinJournalController : ControllerBase
{
    private readonly IMediator _mediator;

    public KurinJournalController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// A page of entries, newest first, filtered by area, person, actor and period
    /// (<paramref name="fromUtc"/> inclusive, <paramref name="toUtc"/> exclusive); at most 50 per page.
    /// </summary>
    [Authorize(Policy = AuthorizationPolicies.RequireUser)]
    [HttpGet]
    [ResourceAuthorize(ResourceType.KurinJournal, ResourceAction.Read, "route:kurinKey")]
    [ProducesResponseType(typeof(KurinJournalPageResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Get(
        Guid kurinKey,
        [FromQuery] AuditArea? area,
        [FromQuery] Guid? memberKey,
        [FromQuery] Guid? actorUserKey,
        [FromQuery] DateTime? fromUtc,
        [FromQuery] DateTime? toUtc,
        [FromQuery] int skip = 0,
        [FromQuery] int take = 20,
        CancellationToken cancellationToken = default)
    {
        var result = await _mediator.Send(
            new GetKurinJournalQuery(kurinKey, area, memberKey, actorUserKey, fromUtc, toUtc, skip, take),
            cancellationToken);
        return result.ToActionResult(this);
    }
}
