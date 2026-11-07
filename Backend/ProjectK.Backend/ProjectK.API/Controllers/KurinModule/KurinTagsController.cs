using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ProjectK.API.Authorization;
using ProjectK.API.Extensions;
using ProjectK.API.Helpers;
using ProjectK.BusinessLogic.Modules.KurinModule.Features.Tag;
using ProjectK.Common.Models.Dtos.KurinModule;
using ProjectK.Common.Models.Dtos.KurinModule.Requests;
using ProjectK.Common.Models.Enums;

namespace ProjectK.API.Controllers.KurinModule;

/// <summary>
/// The kurin's tags: what they are, who carries them, and the kurin's switches. Defining tags and
/// putting them on people is <c>MemberTag:Update</c>, which the kurin's policy gives (the Звʼязковий
/// by default); the switches are kurin settings.
/// </summary>
[ApiController]
public class KurinTagsController : ControllerBase
{
    private readonly IMediator _mediator;

    public KurinTagsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>The kurin's tags, for anyone in it. Empty, with <c>enabled: false</c>, when the kurin has tags off.</summary>
    [Authorize(Policy = AuthorizationPolicies.RequireUser)]
    [HttpGet("api/kurin/{kurinKey:guid}/tags")]
    [ResourceAuthorize(ResourceType.Kurin, ResourceAction.Read, "route:kurinKey")]
    [ProducesResponseType(typeof(KurinTagsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetTags(Guid kurinKey, CancellationToken cancellationToken)
    {
        var response = await _mediator.Send(new GetKurinTagsQuery(kurinKey), cancellationToken);
        return response.ToActionResult(this);
    }

    /// <summary>Creates a tag. The name is unique in the kurin; the colour is one of the palette's.</summary>
    [Authorize(Policy = AuthorizationPolicies.RequireUser)]
    [HttpPost("api/kurin/{kurinKey:guid}/tags")]
    [ResourceAuthorize(ResourceType.MemberTag, ResourceAction.Update, "route:kurinKey", ResourceType.Kurin)]
    [ProducesResponseType(typeof(KurinTagDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateTag(Guid kurinKey, [FromBody] UpsertKurinTagRequest body, CancellationToken cancellationToken)
    {
        var response = await _mediator.Send(new UpsertKurinTagCommand(kurinKey, null, body.Name, body.ColorHex), cancellationToken);
        return response.ToActionResult(this);
    }

    /// <summary>Renames or recolours a tag.</summary>
    [Authorize(Policy = AuthorizationPolicies.RequireUser)]
    [HttpPut("api/kurin/{kurinKey:guid}/tags/{tagKey:guid}")]
    [ResourceAuthorize(ResourceType.MemberTag, ResourceAction.Update, "route:kurinKey", ResourceType.Kurin)]
    [ProducesResponseType(typeof(KurinTagDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> UpdateTag(
        Guid kurinKey,
        Guid tagKey,
        [FromBody] UpsertKurinTagRequest body,
        CancellationToken cancellationToken)
    {
        var response = await _mediator.Send(new UpsertKurinTagCommand(kurinKey, tagKey, body.Name, body.ColorHex), cancellationToken);
        return response.ToActionResult(this);
    }

    /// <summary>Deletes a tag and takes it off everyone who carried it.</summary>
    [Authorize(Policy = AuthorizationPolicies.RequireUser)]
    [HttpDelete("api/kurin/{kurinKey:guid}/tags/{tagKey:guid}")]
    [ResourceAuthorize(ResourceType.MemberTag, ResourceAction.Update, "route:kurinKey", ResourceType.Kurin)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteTag(Guid kurinKey, Guid tagKey, CancellationToken cancellationToken)
    {
        var response = await _mediator.Send(new DeleteKurinTagCommand(kurinKey, tagKey), cancellationToken);
        return response.ToActionResult(this);
    }

    /// <summary>Makes the people named exactly those who carry the tag.</summary>
    [Authorize(Policy = AuthorizationPolicies.RequireUser)]
    [HttpPut("api/kurin/{kurinKey:guid}/tags/{tagKey:guid}/members")]
    [ResourceAuthorize(ResourceType.MemberTag, ResourceAction.Update, "route:kurinKey", ResourceType.Kurin)]
    [ProducesResponseType(typeof(KurinTagDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SetTagMembers(
        Guid kurinKey,
        Guid tagKey,
        [FromBody] SetTagMembersRequest body,
        CancellationToken cancellationToken)
    {
        var response = await _mediator.Send(new SetTagMembersCommand(kurinKey, tagKey, body.MemberKeys), cancellationToken);
        return response.ToActionResult(this);
    }

    /// <summary>Makes the tags named exactly those the person carries in the kurin the caller acts in.</summary>
    [Authorize(Policy = AuthorizationPolicies.RequireUser)]
    [HttpPut("api/member/{memberKey:guid}/tags")]
    [ResourceAuthorize(ResourceType.MemberTag, ResourceAction.Update, "route:memberKey", ResourceType.Member)]
    [ProducesResponseType(typeof(IReadOnlyList<Guid>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> SetMemberTags(Guid memberKey, [FromBody] SetMemberTagsRequest body, CancellationToken cancellationToken)
    {
        var response = await _mediator.Send(new SetMemberTagsCommand(memberKey, body.TagKeys, body.KurinKey), cancellationToken);
        return response.ToActionResult(this);
    }

    /// <summary>The kurin's tag switches: on or off, and whether people see their own.</summary>
    [Authorize(Policy = AuthorizationPolicies.RequireUser)]
    [HttpGet("api/kurin/{kurinKey:guid}/tag-settings")]
    [ResourceAuthorize(ResourceType.KurinSettings, ResourceAction.Read, "route:kurinKey")]
    [ProducesResponseType(typeof(KurinTagSettingsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetTagSettings(Guid kurinKey, CancellationToken cancellationToken)
    {
        var response = await _mediator.Send(new GetKurinTagSettingsQuery(kurinKey), cancellationToken);
        return response.ToActionResult(this);
    }

    /// <summary>Turns tags on or off and says whether people see their own. Off keeps every tag for later.</summary>
    [Authorize(Policy = AuthorizationPolicies.RequireUser)]
    [HttpPut("api/kurin/{kurinKey:guid}/tag-settings")]
    [ResourceAuthorize(ResourceType.Kurin, ResourceAction.Update, "route:kurinKey")]
    [ProducesResponseType(typeof(KurinTagSettingsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SetTagSettings(
        Guid kurinKey,
        [FromBody] SetKurinTagSettingsRequest body,
        CancellationToken cancellationToken)
    {
        var response = await _mediator.Send(new SetKurinTagSettingsCommand(kurinKey, body.Enabled, body.OwnTagsVisible), cancellationToken);
        return response.ToActionResult(this);
    }
}
