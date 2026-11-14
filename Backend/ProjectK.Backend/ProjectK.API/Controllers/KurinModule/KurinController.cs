using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ProjectK.API.Authorization;
using ProjectK.API.Extensions;
using ProjectK.API.Filters;
using ProjectK.BusinessLogic.Modules.KraiModule.Features.Profile;
using ProjectK.BusinessLogic.Modules.KurinModule.Features.Kurin.Delete;
using ProjectK.BusinessLogic.Modules.KurinModule.Features.Kurin.ExportReportPdf;
using ProjectK.BusinessLogic.Modules.KurinModule.Features.Kurin.Get;
using ProjectK.BusinessLogic.Modules.KurinModule.Features.Kurin.SetKrai;
using ProjectK.BusinessLogic.Modules.KurinModule.Features.Kurin.Upsert;
using ProjectK.BusinessLogic.Modules.KurinModule.Models;
using ProjectK.BusinessLogic.Modules.ProbesAndBadgesModule.Features.Badge.Get;
using ProjectK.BusinessLogic.Modules.ProbesAndBadgesModule.Models;
using ProjectK.Common.Extensions;
using ProjectK.Common.Models.Dtos.KurinModule.Requests;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.KraiProfiles;

namespace ProjectK.API.Controllers.KurinModule;

/// <summary>
/// The kurin itself: its record, its badge review queue, and its report.
/// </summary>
[Route("api/kurin")]
[ApiController]
public class KurinController : ControllerBase
{
    private readonly IMediator _mediator;

    public KurinController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// Lists badge submissions across the kurin that are waiting on a decision: all of them, or those
    /// of the гуртки the caller leads, as far as the kurin lets them review.
    /// </summary>
    [Authorize(Policy = AuthorizationPolicies.RequireUser)]
    [HttpGet("{kurinKey:guid}/badges/review")]
    [ResourceAuthorize(ResourceType.Kurin, ResourceAction.Read, "route:kurinKey")]
    [ProducesResponseType(typeof(IEnumerable<BadgeProgressResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetBadgeReviewQueue(Guid kurinKey)
    {
        var response = await _mediator.Send(new GetBadgeReviewQueueQuery(kurinKey));
        return response.ToActionResult(this);
    }

    /// <summary>
    /// Returns one kurin.
    /// </summary>
    [Authorize(Policy = AuthorizationPolicies.RequireUser)]
    [HttpGet("{kurinKey:guid}")]
    [ResourceAuthorize(ResourceType.Kurin, ResourceAction.Read, "route:kurinKey")]
    [ProducesResponseType(typeof(KurinResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetByKey(Guid kurinKey)
    {
        var request = new GetKurinByKeyQuery(kurinKey);
        var response = await _mediator.Send(request);
        return response.ToActionResult(this);
    }

    /// <summary>
    /// Renders the kurin's report as a PDF.
    /// </summary>
    /// <remarks>
    /// The heaviest read in the API — it walks membership, offices, probes and badges to compose one
    /// document.
    /// </remarks>
    /// <param name="kurinKey">The kurin to report on.</param>
    /// <param name="timeZone">
    /// The reader's IANA zone, so the document prints the hours their own clock showed. Only the
    /// browser knows it, so it says. Absent or unknown prints UTC.
    /// </param>
    /// <param name="cancellationToken">Cancels the render when the caller goes away.</param>
    [Authorize(Policy = AuthorizationPolicies.RequireKurinManagement)]
    [HttpGet("{kurinKey:guid}/report/pdf")]
    [ResourceAuthorize(ResourceType.Kurin, ResourceAction.Read, "route:kurinKey")]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK, "application/pdf")]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ExportReportPdf(
        Guid kurinKey,
        [FromQuery] string? timeZone,
        CancellationToken cancellationToken)
    {
        var response = await _mediator.Send(new ExportKurinReportPdfQuery(kurinKey, timeZone), cancellationToken);
        if (response.Type != ResultType.Success || response.Data is null)
        {
            return response.ToActionResult(this);
        }

        return File(response.Data.Content, "application/pdf", response.Data.FileName);
    }

    /// <summary>
    /// Lists every kurin. Administrators only, since nobody else works above one kurin.
    /// </summary>
    [Authorize(Policy = AuthorizationPolicies.RequireAdmin)]
    [HttpGet("kurins")]
    [ProducesResponseType(typeof(IEnumerable<KurinResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetAll()
    {
        var request = new GetKurinsQuery();
        var response = await _mediator.Send(request);
        return response.ToActionResult(this);
    }

    /// <summary>
    /// Creates a kurin from its number and, optionally, its край (the instance default otherwise).
    /// </summary>
    [Authorize(Policy = AuthorizationPolicies.RequireAdmin)]
    [HttpPost]
    [ProducesResponseType(typeof(KurinResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> Create([FromBody] CreateKurinRequest body)
    {
        var request = new UpsertKurinCommand(body.Number, body.KraiCode);
        var response = await _mediator.Send(request);
        return response.ToActionResult(this);
    }

    /// <summary>
    /// Returns the край profile the kurin works under: which вмілості and проби it sees, its
    /// currency and time zone. Every value is filled in, from the край's profile or from code.
    /// </summary>
    [Authorize(Policy = AuthorizationPolicies.RequireUser)]
    [HttpGet("{kurinKey:guid}/profile")]
    [ResourceAuthorize(ResourceType.Kurin, ResourceAction.Read, "route:kurinKey")]
    [ProducesResponseType(typeof(KraiProfile), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetProfile(Guid kurinKey)
    {
        var response = await _mediator.Send(new GetKurinProfileQuery(kurinKey));
        return response.ToActionResult(this);
    }

    /// <summary>
    /// Moves a kurin to another край. Admin-only; the number must be free in that край.
    /// </summary>
    [Authorize(Policy = AuthorizationPolicies.RequireAdmin)]
    [HttpPut("{kurinKey:guid}/krai")]
    [ProducesResponseType(typeof(KurinResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> SetKrai(Guid kurinKey, [FromBody] SetKurinKraiRequest body)
    {
        var response = await _mediator.Send(new SetKurinKraiCommand(kurinKey, body.KraiCode));
        return response.ToActionResult(this);
    }

    /// <summary>
    /// Rewrites a kurin's record.
    /// </summary>
    [Authorize(Policy = AuthorizationPolicies.RequireUser)]
    [HttpPut("{kurinKey:guid}")]
    [ResourceAuthorize(ResourceType.Kurin, ResourceAction.Update, "route:kurinKey")]
    [ProducesResponseType(typeof(KurinResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> Upsert(Guid kurinKey, [FromBody] UpdateKurinRequest request)
    {
        var command = new UpsertKurinCommand(
            kurinKey,
            request.Number,
            request.Stanytsia,
            request.RegionOrCountry,
            request.NamedAfter,
            request.Description,
            request.ProfileVerificationEnabled)
        {
            ScoreEnabled = request.ScoreEnabled,
            RoleNames = request.RoleNames
        };
        var response = await _mediator.Send(command);
        return response.ToActionResult(this);
    }

    /// <summary>
    /// Deletes a kurin.
    /// </summary>
    [Authorize(Policy = AuthorizationPolicies.RequireUser)]
    [HttpDelete("{kurinKey:guid}")]
    [ResourceAuthorize(ResourceType.Kurin, ResourceAction.Delete, "route:kurinKey")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> Delete(Guid kurinKey)
    {
        var request = new DeleteKurinCommand(kurinKey);
        var response = await _mediator.Send(request);
        return response.ToActionResult(this);
    }
}
