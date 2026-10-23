using System.Text;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using ProjectK.BusinessLogic.Modules.MeModule.Features.CalendarFeed.Render;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Security;

namespace ProjectK.API.Controllers.MeModule;

/// <summary>
/// The calendar link itself, fetched by Google, Apple or Outlook from their own servers. They send no
/// cookie and no header, so the action is anonymous and the token in the path is the whole of the
/// access; the request log masks it (<see cref="PathPrefix"/>).
/// </summary>
[ApiController]
public class CalendarFeedController : ControllerBase
{
    /// <summary>Everything after it is the secret; logs and activity records cut the path here.</summary>
    public const string PathPrefix = SecretPaths.CalendarFeedPrefix;

    private readonly IMediator _mediator;

    public CalendarFeedController(IMediator mediator)
    {
        _mediator = mediator;
    }

    public static string UrlOf(HttpRequest request, string token) =>
        $"{request.Scheme}://{request.Host}{request.PathBase}{PathPrefix}{token}.ics";

    [AllowAnonymous]
    [HttpGet(PathPrefix + "{token}.ics")]
    [HttpHead(PathPrefix + "{token}.ics")]
    [EnableRateLimiting("CalendarFeedLimit")]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK, "text/calendar")]
    [ProducesResponseType(StatusCodes.Status304NotModified)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetFeed(string token, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new RenderCalendarFeedQuery(token), cancellationToken);
        if (result.Type != ResultType.Success || result.Data is null)
        {
            // Nothing in the body: an unknown token says no more than that.
            return NotFound();
        }

        var content = Encoding.UTF8.GetBytes(result.Data.Content);
        var etag = $"\"{SecretHash.Sha256Hex(result.Data.Content)[..32]}\"";
        Response.Headers.ETag = etag;
        Response.Headers.CacheControl = "private, max-age=300";
        if (Request.Headers.IfNoneMatch.Contains(etag))
        {
            return StatusCode(StatusCodes.Status304NotModified);
        }

        return File(content, "text/calendar; charset=utf-8", "lileyka.ics");
    }
}
