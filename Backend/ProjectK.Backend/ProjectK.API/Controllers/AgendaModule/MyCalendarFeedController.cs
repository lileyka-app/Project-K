using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using ProjectK.API.Authorization;
using ProjectK.API.Extensions;
using ProjectK.BusinessLogic.Modules.AgendaModule.Features.CalendarFeed;
using ProjectK.Common.Dtos.AgendaModule;
using ProjectK.Common.Dtos.AgendaModule.Requests;
using ProjectK.Common.Enums.Shared;

namespace ProjectK.API.Controllers.AgendaModule;

/// <summary>
/// The caller's own calendar link: where it stands, issuing it, what it carries, switching it off.
/// The link itself is served anonymously by <see cref="CalendarFeedController"/>.
/// </summary>
[ApiController]
[Route("api/me")]
[Authorize(Policy = AuthorizationPolicies.RequireUser)]
public class MyCalendarFeedController : ControllerBase
{
    private readonly IMediator _mediator;

    public MyCalendarFeedController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>Where the calendar link stands. The link itself is shown only once, when issued.</summary>
    [HttpGet("calendar-feed")]
    [ProducesResponseType(typeof(CalendarFeedStatusDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetCalendarFeed()
    {
        var result = await _mediator.Send(new GetCalendarFeedQuery());
        return result.ToActionResult(this);
    }

    /// <summary>Issues the calendar link, revoking any earlier one, and returns it this once.</summary>
    [HttpPost("calendar-feed")]
    [EnableRateLimiting("AccountSecurityLimit")]
    [ProducesResponseType(typeof(CalendarFeedLinkDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> IssueCalendarFeed([FromBody] CalendarFeedOptionsRequest request)
    {
        var result = await _mediator.Send(new IssueCalendarFeedCommand(request.IncludeSchedules, request.IncludeTasks));
        if (result.Type != ResultType.Success || result.Data is null)
        {
            return result.ToActionResult(this);
        }

        return Ok(new CalendarFeedLinkDto(CalendarFeedController.UrlOf(Request, result.Data.Token), result.Data.Status));
    }

    /// <summary>Changes what the live link carries; the link stays the same.</summary>
    [HttpPatch("calendar-feed")]
    [ProducesResponseType(typeof(CalendarFeedStatusDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateCalendarFeed([FromBody] CalendarFeedOptionsRequest request)
    {
        var result = await _mediator.Send(new UpdateCalendarFeedCommand(request.IncludeSchedules, request.IncludeTasks));
        return result.ToActionResult(this);
    }

    [HttpDelete("calendar-feed")]
    [ProducesResponseType(typeof(CalendarFeedStatusDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> DisableCalendarFeed()
    {
        var result = await _mediator.Send(new DisableCalendarFeedCommand());
        return result.ToActionResult(this);
    }
}
