using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ProjectK.API.Authorization;
using ProjectK.API.Extensions;
using ProjectK.API.Filters;
using ProjectK.API.Models.Requests;
using ProjectK.BusinessLogic.Modules.KurinModule;
using ProjectK.BusinessLogic.Modules.KurinModule.Features.Import;
using ProjectK.BusinessLogic.Modules.KurinModule.Features.Registry;
using ProjectK.Common.Dtos.KurinModule.Requests;
using ProjectK.Common.Extensions;
using ProjectK.Common.Models.Enums;

namespace ProjectK.API.Controllers.KurinModule;

/// <summary>
/// The kurin's roster as a file: an uploaded one read and imported, and the registry written out.
/// </summary>
[Route("api/kurin/{kurinKey:guid}")]
[ApiController]
public class KurinRosterController : ControllerBase
{
    /// <summary>
    /// A kurin's roster is a few dozen rows. Five megabytes is far more than that could ever be,
    /// and is here so a wrong file is refused at the door rather than read into memory.
    /// </summary>
    private const long MaxRosterBytes = 5 * 1024 * 1024;

    private const string SpreadsheetContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    private readonly IMediator _mediator;

    public KurinRosterController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// Reads an uploaded roster and answers with its columns, a guess at what each one means, and
    /// every data row. Writes nothing.
    /// </summary>
    [Authorize(Policy = AuthorizationPolicies.RequireKurinManagement)]
    [HttpPost("import/preview")]
    [ResourceAuthorize(ResourceType.Kurin, ResourceAction.Update, "route:kurinKey")]
    [RequestSizeLimit(MaxRosterBytes)]
    [ProducesResponseType(typeof(RosterPreview), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> PreviewRosterImport(
        Guid kurinKey,
        [FromForm] UploadRosterRequest request,
        CancellationToken cancellationToken)
    {
        if (request.File is null || request.File.Length == 0)
        {
            return this.Failure(ResultType.BadRequest, KurinErrors.NoFile, "No file was uploaded.");
        }

        await using var content = request.File.OpenReadStream();
        var result = await _mediator.Send(new PreviewRosterImportQuery(content), cancellationToken);
        return result.ToActionResult(this);
    }

    /// <summary>
    /// Applies a mapped roster to the kurin — or, with <c>dryRun</c>, only reports what it would do.
    /// </summary>
    /// <remarks>
    /// One transaction: either every usable row lands or none does. A half-imported kurin is worse
    /// than an unimported one, because nobody can tell which half is missing.
    /// </remarks>
    [Authorize(Policy = AuthorizationPolicies.RequireKurinManagement)]
    [HttpPost("import")]
    [ResourceAuthorize(ResourceType.Kurin, ResourceAction.Update, "route:kurinKey")]
    [ProducesResponseType(typeof(RosterImportReport), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ImportRoster(
        Guid kurinKey,
        [FromBody] ApplyRosterRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(
            new ImportRosterCommand(
                kurinKey,
                request.Rows,
                request.Mapping,
                request.CreateMissingGroups,
                request.DryRun),
            cancellationToken);

        return result.ToActionResult(this);
    }

    /// <summary>
    /// The kurin's registry as a spreadsheet, in the columns the caller asked for.
    /// </summary>
    /// <remarks>
    /// Answered from the same read the screen uses, so a field the caller may not see on screen is
    /// not in the file either. Nothing is exported that could not simply be looked at.
    /// </remarks>
    [Authorize(Policy = AuthorizationPolicies.RequireGroupLeadership)]
    [HttpPost("registry/export")]
    [ResourceAuthorize(ResourceType.Kurin, ResourceAction.Read, "route:kurinKey")]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK, SpreadsheetContentType)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ExportRegistry(
        Guid kurinKey,
        [FromBody] ExportRegistryRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(
            new ExportRegistryQuery(kurinKey, request.Columns ?? []),
            cancellationToken);

        if (result.Type != ResultType.Success || result.Data is null)
        {
            return result.ToActionResult(this);
        }

        return File(result.Data.Content, SpreadsheetContentType, result.Data.FileName);
    }
}
