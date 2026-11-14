using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using ProjectK.API.Authorization;
using ProjectK.API.Extensions;
using ProjectK.API.Uploads;
using ProjectK.BusinessLogic.Modules.InfrastructureModule.Feedback.ReportProblem;
using ProjectK.BusinessLogic.Modules.InfrastructureModule.Feedback.UploadScreenshot;
using ProjectK.Common.Models.Dtos.InfrastructureModule;
using ProjectK.Common.Models.Dtos.InfrastructureModule.Requests;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Records;

namespace ProjectK.API.Controllers.InfrastructureModule;

/// <summary>
/// «Повідомити про проблему»: any signed-in person, from any page. The report becomes a GitHub
/// issue on the server's token; the browser never talks to GitHub. Both actions sit under the
/// account-security rate limit, because a public tracker is somewhere spam would be seen.
/// </summary>
[ApiController]
[Route("api/feedback")]
public class FeedbackController : ControllerBase
{
    private readonly IMediator _mediator;

    public FeedbackController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// Files a problem report. The browser's user agent is read from the request, not trusted from the body.
    /// </summary>
    [Authorize(Policy = AuthorizationPolicies.RequireUser)]
    [EnableRateLimiting("AccountSecurityLimit")]
    [HttpPost("problems")]
    [ProducesResponseType(typeof(ProblemReportReceipt), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> ReportProblem([FromBody] ReportProblemRequest request, CancellationToken cancellationToken)
    {
        var command = new ReportProblemCommand(
            request.Title,
            request.Description,
            request.Steps,
            request.Expected,
            request.Route,
            request.AppVersion,
            Request.Headers.UserAgent.ToString(),
            request.ScreenshotUrls);

        var response = await _mediator.Send(command, cancellationToken);
        return response.ToActionResult(this);
    }

    /// <summary>
    /// Stores one screenshot for a report and returns the address the report will embed.
    /// </summary>
    [Authorize(Policy = AuthorizationPolicies.RequireUser)]
    [EnableRateLimiting("AccountSecurityLimit")]
    [HttpPost("screenshots")]
    [RequestSizeLimit(ImageUploadRules.MaxRequestBytes)]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(ScreenshotUploadedDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> UploadScreenshot(IFormFile? file, CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
        {
            return this.Failure(ResultType.BadRequest, "FileRequired", "Attach an image.");
        }

        if (ImageUploadRules.Refusal(file) is { } refusal)
        {
            return this.Failure(ResultType.BadRequest, refusal.Code, refusal.Message);
        }

        await using var content = file.OpenReadStream();
        var response = await _mediator.Send(new UploadFeedbackScreenshotCommand(content, file.FileName), cancellationToken);
        if (response.Type != ResultType.Success || response.Data is null)
        {
            return response.ToActionResult(this);
        }

        return Ok(new ScreenshotUploadedDto(response.Data));
    }
}
