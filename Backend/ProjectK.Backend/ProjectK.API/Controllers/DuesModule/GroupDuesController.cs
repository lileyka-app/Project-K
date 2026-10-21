using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProjectK.API.Authorization;
using ProjectK.API.Extensions;
using ProjectK.API.Helpers;
using ProjectK.BusinessLogic.Modules.DuesModule.Features.Entry.Create;
using ProjectK.BusinessLogic.Modules.DuesModule.Features.Entry.Delete;
using ProjectK.BusinessLogic.Modules.DuesModule.Features.Entry.Update;
using ProjectK.BusinessLogic.Modules.DuesModule.Features.Entry.Verify;
using ProjectK.BusinessLogic.Modules.DuesModule.Features.Group.Get;
using ProjectK.BusinessLogic.Modules.DuesModule.Features.Group.Grants;
using ProjectK.BusinessLogic.Modules.DuesModule.Features.Group.Grants;
using ProjectK.BusinessLogic.Modules.DuesModule.Features.Group.SetConcession;
using ProjectK.BusinessLogic.Modules.DuesModule.Features.Group.SetRate;
using ProjectK.BusinessLogic.Modules.DuesModule.Models;
using ProjectK.Common.Models.Dtos.DuesModule.Requests;
using ProjectK.Common.Models.Enums;

namespace ProjectK.API.Controllers.DuesModule;

/// <summary>
/// A гурток's вкладка. Every route is keyed by the гурток, and the access check reads the scope from
/// it: whoever keeps that гурток's box gets in, nobody else.
/// </summary>
[ApiController]
[Route("api/group/{groupKey:guid}/dues")]
[Authorize(Policy = AuthorizationPolicies.RequireUser)]
public class GroupDuesController : ControllerBase
{
    private readonly IMediator _mediator;

    public GroupDuesController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet]
    [ResourceAuthorize(ResourceType.GroupDues, ResourceAction.Read, "route:groupKey")]
    [ProducesResponseType(typeof(GroupDuesResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(Guid groupKey)
    {
        var result = await _mediator.Send(new GetGroupDuesQuery(groupKey));
        return result.ToActionResult(this);
    }

    [HttpPut("rate")]
    [ResourceAuthorize(ResourceType.GroupDues, ResourceAction.Update, "route:groupKey")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> SetRate(Guid groupKey, [FromBody] SetGroupDuesRateRequest request)
    {
        var result = await _mediator.Send(new SetGroupDuesRateCommand(groupKey, request));
        return result.ToActionResult(this);
    }

    [HttpPut("members/{membershipKey:guid}/concession")]
    [ResourceAuthorize(ResourceType.GroupDues, ResourceAction.Update, "route:groupKey")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SetConcession(Guid groupKey, Guid membershipKey, [FromBody] SetDuesConcessionRequest request)
    {
        var result = await _mediator.Send(new SetDuesConcessionCommand(groupKey, membershipKey, request));
        return result.ToActionResult(this);
    }

    /// <summary>Gives a youth one of the край's пільги, from a quarter on.</summary>
    [HttpPost("members/{membershipKey:guid}/concessions")]
    [ResourceAuthorize(ResourceType.GroupDues, ResourceAction.Update, "route:groupKey")]
    [ProducesResponseType(typeof(Guid), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> GrantConcession(Guid groupKey, Guid membershipKey, [FromBody] GrantDuesConcessionRequest request)
    {
        var result = await _mediator.Send(new GrantDuesConcessionCommand(groupKey, membershipKey, request));
        return result.ToActionResult(this);
    }

    /// <summary>Sets when a given пільга ends, or why it was given.</summary>
    [HttpPut("concessions/{grantKey:guid}")]
    [ResourceAuthorize(ResourceType.GroupDues, ResourceAction.Update, "route:groupKey")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateConcessionGrant(Guid groupKey, Guid grantKey, [FromBody] UpdateDuesConcessionGrantRequest request)
    {
        var result = await _mediator.Send(new UpdateDuesConcessionGrantCommand(groupKey, grantKey, request));
        return result.ToActionResult(this);
    }

    /// <summary>Removes a пільга given by mistake.</summary>
    [HttpDelete("concessions/{grantKey:guid}")]
    [ResourceAuthorize(ResourceType.GroupDues, ResourceAction.Update, "route:groupKey")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteConcessionGrant(Guid groupKey, Guid grantKey)
    {
        var result = await _mediator.Send(new DeleteDuesConcessionGrantCommand(groupKey, grantKey));
        return result.ToActionResult(this);
    }

    [HttpPost("entries")]
    [ResourceAuthorize(ResourceType.GroupDues, ResourceAction.Create, "route:groupKey")]
    [ProducesResponseType(typeof(Guid), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateEntry(Guid groupKey, [FromBody] UpsertDuesEntryRequest request)
    {
        var result = await _mediator.Send(new CreateDuesEntryCommand(groupKey, request));
        return result.ToActionResult(this);
    }

    [HttpPut("entries/{entryKey:guid}")]
    [ResourceAuthorize(ResourceType.GroupDues, ResourceAction.Update, "route:groupKey")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> UpdateEntry(Guid groupKey, Guid entryKey, [FromBody] UpsertDuesEntryRequest request)
    {
        var result = await _mediator.Send(new UpdateDuesEntryCommand(groupKey, entryKey, request));
        return result.ToActionResult(this);
    }

    [HttpDelete("entries/{entryKey:guid}")]
    [ResourceAuthorize(ResourceType.GroupDues, ResourceAction.Delete, "route:groupKey")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> DeleteEntry(Guid groupKey, Guid entryKey)
    {
        var result = await _mediator.Send(new DeleteDuesEntryCommand(groupKey, entryKey));
        return result.ToActionResult(this);
    }

    [HttpPut("entries/{entryKey:guid}/verified")]
    [ResourceAuthorize(ResourceType.GroupDues, ResourceAction.Manage, "route:groupKey")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SetEntryVerified(Guid groupKey, Guid entryKey, [FromBody] SetDuesEntryVerifiedRequest request)
    {
        var result = await _mediator.Send(new SetDuesEntryVerifiedCommand(groupKey, entryKey, request.IsVerified));
        return result.ToActionResult(this);
    }
}
