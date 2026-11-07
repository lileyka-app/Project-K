using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProjectK.API.Authorization;
using ProjectK.API.Extensions;
using ProjectK.API.Helpers;
using ProjectK.BusinessLogic.Modules.DuesModule.Features.Group.Grants;
using ProjectK.BusinessLogic.Modules.DuesModule.Features.Group.SetConcession;
using ProjectK.BusinessLogic.Modules.DuesModule.Features.Kurin.Entry;
using ProjectK.BusinessLogic.Modules.DuesModule.Features.Kurin.Get;
using ProjectK.BusinessLogic.Modules.DuesModule.Features.Kurin.Groups;
using ProjectK.BusinessLogic.Modules.DuesModule.Features.Kurin.Receive;
using ProjectK.BusinessLogic.Modules.DuesModule.Features.Kurin.SetRate;
using ProjectK.BusinessLogic.Modules.DuesModule.Features.Kurin.SetSettings;
using ProjectK.BusinessLogic.Modules.DuesModule.Models;
using ProjectK.Common.Models.Dtos.DuesModule.Requests;
using ProjectK.Common.Models.Enums;

namespace ProjectK.API.Controllers.DuesModule;

/// <summary>
/// The kurin's side of the вкладка: the rates every гурток charges by, the kurin's own box, and the
/// transfers гуртки hand up — and in a kurin without гуртки, every person's own account in that box.
/// Every route is keyed by the kurin, and the access check reads the scope from it.
/// </summary>
[ApiController]
[Route("api/kurin/{kurinKey:guid}/dues")]
[Authorize(Policy = AuthorizationPolicies.RequireUser)]
public class KurinDuesController : ControllerBase
{
    private readonly IMediator _mediator;

    public KurinDuesController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet]
    [ResourceAuthorize(ResourceType.KurinDues, ResourceAction.Read, "route:kurinKey")]
    [ProducesResponseType(typeof(KurinDuesResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Get(Guid kurinKey)
    {
        var result = await _mediator.Send(new GetKurinDuesQuery(kurinKey));
        return result.ToActionResult(this);
    }

    /// <summary>The гуртки whose box the caller may open — any member may ask; a youth gets none.</summary>
    [HttpGet("groups")]
    [ResourceAuthorize(ResourceType.Kurin, ResourceAction.Read, "route:kurinKey")]
    [ProducesResponseType(typeof(IReadOnlyList<DuesGroupLinkDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetReadableGroups(Guid kurinKey)
    {
        var result = await _mediator.Send(new GetReadableDuesGroupsQuery(kurinKey));
        return result.ToActionResult(this);
    }

    [HttpPut("rate")]
    [ResourceAuthorize(ResourceType.KurinDues, ResourceAction.Update, "route:kurinKey")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> SetRate(Guid kurinKey, [FromBody] SetKurinDuesRateRequest request)
    {
        var result = await _mediator.Send(new SetKurinDuesRateCommand(kurinKey, request));
        return result.ToActionResult(this);
    }

    /// <summary>When the kurin's вкладка falls due: наперед or за пройдений квартал. Whoever sets the rates sets this.</summary>
    [HttpPut("settings")]
    [ResourceAuthorize(ResourceType.KurinDues, ResourceAction.Update, "route:kurinKey")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> SetSettings(Guid kurinKey, [FromBody] SetKurinDuesSettingsRequest request)
    {
        var result = await _mediator.Send(new SetKurinDuesSettingsCommand(kurinKey, request.ChargeTiming));
        return result.ToActionResult(this);
    }

    [HttpPut("transfers/{entryKey:guid}/received")]
    [ResourceAuthorize(ResourceType.KurinDues, ResourceAction.Update, "route:kurinKey")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SetTransferReceived(Guid kurinKey, Guid entryKey, [FromBody] SetDuesTransferReceivedRequest request)
    {
        var result = await _mediator.Send(new SetDuesTransferReceivedCommand(kurinKey, entryKey, request.IsReceived));
        return result.ToActionResult(this);
    }

    [HttpPost("entries")]
    [ResourceAuthorize(ResourceType.KurinDues, ResourceAction.Create, "route:kurinKey")]
    [ProducesResponseType(typeof(Guid), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateEntry(Guid kurinKey, [FromBody] UpsertDuesEntryRequest request)
    {
        var result = await _mediator.Send(new CreateKurinDuesEntryCommand(kurinKey, request));
        return result.ToActionResult(this);
    }

    [HttpPut("entries/{entryKey:guid}")]
    [ResourceAuthorize(ResourceType.KurinDues, ResourceAction.Update, "route:kurinKey")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> UpdateEntry(Guid kurinKey, Guid entryKey, [FromBody] UpsertDuesEntryRequest request)
    {
        var result = await _mediator.Send(new UpdateKurinDuesEntryCommand(kurinKey, entryKey, request));
        return result.ToActionResult(this);
    }

    [HttpDelete("entries/{entryKey:guid}")]
    [ResourceAuthorize(ResourceType.KurinDues, ResourceAction.Delete, "route:kurinKey")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> DeleteEntry(Guid kurinKey, Guid entryKey)
    {
        var result = await _mediator.Send(new DeleteKurinDuesEntryCommand(kurinKey, entryKey));
        return result.ToActionResult(this);
    }

    [HttpPut("entries/{entryKey:guid}/verified")]
    [ResourceAuthorize(ResourceType.KurinDues, ResourceAction.Manage, "route:kurinKey")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SetEntryVerified(Guid kurinKey, Guid entryKey, [FromBody] SetDuesEntryVerifiedRequest request)
    {
        var result = await _mediator.Send(new SetKurinDuesEntryVerifiedCommand(kurinKey, entryKey, request.IsVerified));
        return result.ToActionResult(this);
    }

    /// <summary>The kurin's пільга for a person with an account in its own box, from a quarter on.</summary>
    [HttpPut("members/{membershipKey:guid}/concession")]
    [ResourceAuthorize(ResourceType.KurinDues, ResourceAction.Update, "route:kurinKey")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SetConcession(Guid kurinKey, Guid membershipKey, [FromBody] SetDuesConcessionRequest request)
    {
        var result = await _mediator.Send(new SetDuesConcessionCommand(kurinKey, membershipKey, request));
        return result.ToActionResult(this);
    }

    /// <summary>Gives a person with an account in the kurin's own box one of the край's пільги.</summary>
    [HttpPost("members/{membershipKey:guid}/concessions")]
    [ResourceAuthorize(ResourceType.KurinDues, ResourceAction.Update, "route:kurinKey")]
    [ProducesResponseType(typeof(Guid), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> GrantConcession(Guid kurinKey, Guid membershipKey, [FromBody] GrantDuesConcessionRequest request)
    {
        var result = await _mediator.Send(new GrantDuesConcessionCommand(kurinKey, membershipKey, request));
        return result.ToActionResult(this);
    }

    [HttpPut("concessions/{grantKey:guid}")]
    [ResourceAuthorize(ResourceType.KurinDues, ResourceAction.Update, "route:kurinKey")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateConcessionGrant(Guid kurinKey, Guid grantKey, [FromBody] UpdateDuesConcessionGrantRequest request)
    {
        var result = await _mediator.Send(new UpdateDuesConcessionGrantCommand(kurinKey, grantKey, request));
        return result.ToActionResult(this);
    }

    [HttpDelete("concessions/{grantKey:guid}")]
    [ResourceAuthorize(ResourceType.KurinDues, ResourceAction.Update, "route:kurinKey")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteConcessionGrant(Guid kurinKey, Guid grantKey)
    {
        var result = await _mediator.Send(new DeleteDuesConcessionGrantCommand(kurinKey, grantKey));
        return result.ToActionResult(this);
    }
}
