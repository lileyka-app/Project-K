using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using ProjectK.API.Authorization;
using ProjectK.API.Extensions;
using ProjectK.API.Helpers;
using ProjectK.BusinessLogic.Modules.MeModule.Features.CalendarFeed.Disable;
using ProjectK.BusinessLogic.Modules.MeModule.Features.CalendarFeed.Get;
using ProjectK.BusinessLogic.Modules.MeModule.Features.CalendarFeed.Issue;
using ProjectK.BusinessLogic.Modules.MeModule.Features.CalendarFeed.Update;
using ProjectK.BusinessLogic.Modules.MeModule.Features.Dues;
using ProjectK.BusinessLogic.Modules.MeModule.Features.Duties;
using ProjectK.BusinessLogic.Modules.MeModule.Features.Events;
using ProjectK.BusinessLogic.Modules.MeModule.Features.Groups;
using ProjectK.BusinessLogic.Modules.MeModule.Features.Growth;
using ProjectK.BusinessLogic.Modules.MeModule.Features.Score;
using ProjectK.BusinessLogic.Modules.MeModule.Features.Tasks;
using ProjectK.BusinessLogic.Modules.MeModule.Models;
using ProjectK.Common.Models.Enums;

namespace ProjectK.API.Controllers.MeModule;

/// <summary>
/// The person behind the token, across every kurin they stand in — what the dashboard reads. No
/// key in any route: there is nobody else to ask about, so there is nothing to authorize beyond
/// being signed in. What each kurin lets them see is settled per kurin inside.
/// </summary>
[ApiController]
[Route("api/me")]
[Authorize(Policy = AuthorizationPolicies.RequireUser)]
public class MeController : ControllerBase
{
    private readonly IMediator _mediator;

    public MeController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>Events ahead in the next <paramref name="days"/> (14 by default), with the person's own answers.</summary>
    [HttpGet("events")]
    [ProducesResponseType(typeof(IReadOnlyList<MyEventDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetEvents([FromQuery] int days = 14)
    {
        var result = await _mediator.Send(new GetMyEventsQuery(days));
        return result.ToActionResult(this);
    }

    [HttpPut("events/{agendaItemKey:guid}/response")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SetEventResponse(Guid agendaItemKey, [FromBody] SetMyEventResponseRequest request)
    {
        var result = await _mediator.Send(new SetMyEventResponseCommand(agendaItemKey, request.OccurrenceStartUtc, request.Status ?? AgendaRsvpStatus.Going));
        return result.ToActionResult(this);
    }

    /// <summary>Open tasks the person is on the hook for, or raised, in every kurin.</summary>
    [HttpGet("tasks")]
    [ProducesResponseType(typeof(IReadOnlyList<MyTaskDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetTasks()
    {
        var result = await _mediator.Send(new GetMyTasksQuery());
        return result.ToActionResult(this);
    }

    /// <summary>The проба in hand and the вмілості — empty, with the flag off, for anyone outside the youth programme.</summary>
    [HttpGet("growth")]
    [ProducesResponseType(typeof(MyGrowthDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetGrowth()
    {
        var result = await _mediator.Send(new GetMyGrowthQuery());
        return result.ToActionResult(this);
    }

    /// <summary>The вкладка in every kurin that charges the person.</summary>
    [HttpGet("dues")]
    [ProducesResponseType(typeof(IReadOnlyList<MyDuesDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetDues()
    {
        var result = await _mediator.Send(new GetMyDuesQuery());
        return result.ToActionResult(this);
    }

    /// <summary>What waits on the person as провід, per kurin where they hold an office; empty for anyone else.</summary>
    [HttpGet("duties")]
    [ProducesResponseType(typeof(IReadOnlyList<MyDutyDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetDuties()
    {
        var result = await _mediator.Send(new GetMyDutiesQuery());
        return result.ToActionResult(this);
    }

    /// <summary>The гуртки the person is in or leads, in every kurin, with their сильветки.</summary>
    [HttpGet("groups")]
    [ProducesResponseType(typeof(IReadOnlyList<MyGroupDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetGroups()
    {
        var result = await _mediator.Send(new GetMyGroupsQuery());
        return result.ToActionResult(this);
    }

    /// <summary>Own points this пластовий рік, per kurin where the person is a youth in a гурток.</summary>
    [HttpGet("score")]
    [ProducesResponseType(typeof(IReadOnlyList<MyScoreDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetScore()
    {
        var result = await _mediator.Send(new GetMyScoreQuery());
        return result.ToActionResult(this);
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
    [ProducesResponseType(typeof(IssuedCalendarFeedResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> IssueCalendarFeed([FromBody] CalendarFeedOptionsRequest request)
    {
        var result = await _mediator.Send(new IssueCalendarFeedCommand(request.IncludeSchedules, request.IncludeTasks));
        if (result.Type != ResultType.Success || result.Data is null)
        {
            return result.ToActionResult(this);
        }

        return Ok(new IssuedCalendarFeedResponse(CalendarFeedController.UrlOf(Request, result.Data.Token), result.Data.Status));
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

/// <summary>Body for a dashboard RSVP; <c>OccurrenceStartUtc</c> names the occurrence of a series, null for a one-off event.</summary>
public sealed record SetMyEventResponseRequest(AgendaRsvpStatus? Status, DateTime? OccurrenceStartUtc = null);

public sealed record CalendarFeedOptionsRequest(bool IncludeSchedules, bool IncludeTasks);

/// <summary>A just-issued calendar link: the only answer that ever carries it.</summary>
public sealed record IssuedCalendarFeedResponse(string FeedUrl, CalendarFeedStatusDto Status);
