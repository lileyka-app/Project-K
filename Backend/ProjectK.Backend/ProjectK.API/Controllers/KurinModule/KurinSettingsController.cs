using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ProjectK.API.Authorization;
using ProjectK.API.Extensions;
using ProjectK.API.Filters;
using ProjectK.BusinessLogic.Modules.KraiModule.Features.Profile;
using ProjectK.BusinessLogic.Modules.KurinModule.Features.Group;
using ProjectK.BusinessLogic.Modules.KurinModule.Features.Kurin;
using ProjectK.BusinessLogic.Modules.KurinModule.Models;
using ProjectK.Common.Extensions;
using ProjectK.Common.Models.Dtos.KurinModule;
using ProjectK.Common.Models.Dtos.KurinModule.Requests;
using ProjectK.Common.Models.Enums;

namespace ProjectK.API.Controllers.KurinModule;

/// <summary>
/// A kurin's settings: the card fields it hides, what its minors may share, its probe editions and
/// its «Права й видимість».
/// </summary>
[Route("api/kurin/{kurinKey:guid}")]
[ApiController]
public class KurinSettingsController : ControllerBase
{
    private readonly IMediator _mediator;

    public KurinSettingsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// The card fields the kurin may leave off: what its край makes optional or adds without
    /// requiring. Each says whether the kurin hides it.
    /// </summary>
    [Authorize(Policy = AuthorizationPolicies.RequireUser)]
    [HttpGet("card-fields")]
    [ResourceAuthorize(ResourceType.KurinSettings, ResourceAction.Read, "route:kurinKey")]
    [ProducesResponseType(typeof(IReadOnlyList<KurinCardFieldResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetCardFields(Guid kurinKey)
    {
        var response = await _mediator.Send(new GetKurinCardFieldsQuery(kurinKey));
        return response.ToActionResult(this);
    }

    /// <summary>Sets which of those fields the kurin hides; a field it may not hide is ignored.</summary>
    [Authorize(Policy = AuthorizationPolicies.RequireUser)]
    [HttpPut("card-fields")]
    [ResourceAuthorize(ResourceType.Kurin, ResourceAction.Update, "route:kurinKey")]
    [ProducesResponseType(typeof(IReadOnlyList<KurinCardFieldResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SetCardFields(Guid kurinKey, [FromBody] SetKurinCardFieldsRequest body)
    {
        var response = await _mediator.Send(new SetKurinCardFieldsCommand(kurinKey, body.Hidden ?? []));
        return response.ToActionResult(this);
    }

    /// <summary>
    /// Every гурток's edition of the probe programme, for the kurin's settings.
    /// </summary>
    /// <remarks>
    /// Carries the editions the catalogue offers the kurin's край and, per гурток, each youth's
    /// standing: a closed probe past the first holds them to its edition, one only under way moves
    /// with the гурток. That is progress data, hence the same gate as the rest of the settings.
    /// A гурток switches through <c>PUT api/group/{groupKey}/probe-edition</c>.
    /// </remarks>
    [Authorize(Policy = AuthorizationPolicies.RequireUser)]
    [HttpGet("probe-editions")]
    [ResourceAuthorize(ResourceType.KurinSettings, ResourceAction.Read, "route:kurinKey")]
    [ProducesResponseType(typeof(KurinProbeEditionsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetProbeEditions(Guid kurinKey, CancellationToken cancellationToken)
    {
        var response = await _mediator.Send(new GetKurinProbeEditionsQuery(kurinKey), cancellationToken);
        return response.ToActionResult(this);
    }

    /// <summary>What the kurin lets its people under 18 show on a shared card.</summary>
    [Authorize(Policy = AuthorizationPolicies.RequireUser)]
    [HttpGet("card-sharing")]
    [ResourceAuthorize(ResourceType.KurinSettings, ResourceAction.Read, "route:kurinKey")]
    [ProducesResponseType(typeof(KurinCardSharingDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetCardSharing(Guid kurinKey, CancellationToken cancellationToken)
    {
        var response = await _mediator.Send(new GetKurinCardSharingQuery(kurinKey), cancellationToken);
        return response.ToActionResult(this);
    }

    /// <summary>Sets what the kurin lets its people under 18 show; those 18 or older decide alone.</summary>
    [Authorize(Policy = AuthorizationPolicies.RequireKurinManagement)]
    [HttpPut("card-sharing")]
    [ResourceAuthorize(ResourceType.Kurin, ResourceAction.Update, "route:kurinKey")]
    [ProducesResponseType(typeof(KurinCardSharingDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> SetCardSharing(Guid kurinKey, [FromBody] SetKurinCardSharingRequest body, CancellationToken cancellationToken)
    {
        var response = await _mediator.Send(
            new SetKurinCardSharingCommand(kurinKey, body.MinorsMayShare, body.MinorFields, body.MinorsMayLink), cancellationToken);
        return response.ToActionResult(this);
    }

    /// <summary>
    /// The kurin's «Права й видимість»: for every capability and audience, what the kurin has now,
    /// what the default is, and whether the cell is locked.
    /// </summary>
    [Authorize(Policy = AuthorizationPolicies.RequireUser)]
    [HttpGet("access-policy")]
    [ResourceAuthorize(ResourceType.KurinSettings, ResourceAction.Read, "route:kurinKey")]
    [ProducesResponseType(typeof(KurinAccessPolicyDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetAccessPolicy(Guid kurinKey, CancellationToken cancellationToken)
    {
        var response = await _mediator.Send(new GetKurinAccessPolicyQuery(kurinKey), cancellationToken);
        return response.ToActionResult(this);
    }

    /// <summary>
    /// Saves the kurin's policy from the whole effective matrix. Only what differs from the defaults
    /// is kept; an unknown name or a change to a locked cell is refused with <c>AccessPolicyInvalid</c>.
    /// </summary>
    [Authorize(Policy = AuthorizationPolicies.RequireUser)]
    [HttpPut("access-policy")]
    [ResourceAuthorize(ResourceType.Kurin, ResourceAction.Update, "route:kurinKey")]
    [ProducesResponseType(typeof(KurinAccessPolicyDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SetAccessPolicy(
        Guid kurinKey,
        [FromBody] SetKurinAccessPolicyRequest body,
        CancellationToken cancellationToken)
    {
        var grants = (body.Grants ?? []).ToDictionary(
            row => row.Key,
            row => (IReadOnlyDictionary<string, string?>)(row.Value ?? []));

        var response = await _mediator.Send(new SetKurinAccessPolicyCommand(kurinKey, grants), cancellationToken);
        return response.ToActionResult(this);
    }
}
