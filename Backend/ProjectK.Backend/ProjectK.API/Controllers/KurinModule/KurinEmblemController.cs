using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ProjectK.API.Authorization;
using ProjectK.API.Extensions;
using ProjectK.API.Filters;
using ProjectK.API.Models.Requests;
using ProjectK.API.Uploads;
using ProjectK.BusinessLogic.Modules.KurinModule.Features.Kurin;
using ProjectK.Common.Extensions;
using ProjectK.Common.Models.Dtos.KurinModule;
using ProjectK.Common.Models.Enums;

namespace ProjectK.API.Controllers.KurinModule;

/// <summary>
/// A senior kurin's emblem, shown instead of the УПЮ number plate.
/// </summary>
[Route("api/kurin/{kurinKey:guid}/emblem")]
[ApiController]
public class KurinEmblemController : ControllerBase
{
    private readonly IMediator _mediator;

    public KurinEmblemController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// Stores a senior kurin's emblem. Same types and size as a гурток's silhouette; a УПЮ kurin is
    /// refused.
    /// </summary>
    [Authorize(Policy = AuthorizationPolicies.RequireUser)]
    [HttpPost]
    [ResourceAuthorize(ResourceType.Kurin, ResourceAction.Update, "route:kurinKey")]
    [RequestSizeLimit(ImageUploadRules.MaxRequestBytes)]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(KurinDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public Task<IActionResult> UploadEmblem(Guid kurinKey, [FromForm] UploadImageRequest form, CancellationToken cancellationToken) =>
        this.SendImageAsync(
            form.File,
            (bytes, fileName) => _mediator.Send(new UploadKurinEmblemCommand(kurinKey, bytes, fileName), cancellationToken),
            cancellationToken);

    /// <summary>Takes a senior kurin's emblem off.</summary>
    [Authorize(Policy = AuthorizationPolicies.RequireUser)]
    [HttpDelete]
    [ResourceAuthorize(ResourceType.Kurin, ResourceAction.Update, "route:kurinKey")]
    [ProducesResponseType(typeof(KurinDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteEmblem(Guid kurinKey, CancellationToken cancellationToken)
    {
        var response = await _mediator.Send(new DeleteKurinEmblemCommand(kurinKey), cancellationToken);
        return response.ToActionResult(this);
    }
}
