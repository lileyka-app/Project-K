using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProjectK.API.Authorization;
using ProjectK.API.Extensions;
using ProjectK.BusinessLogic.Modules.InfrastructureModule.Features.Notifications;
using ProjectK.Common.Dtos.InfrastructureModule;
using ProjectK.Common.Dtos.InfrastructureModule.Requests;

namespace ProjectK.API.Controllers.InfrastructureModule;

/// <summary>
/// The caller's own notification inbox. Every action is scoped to the signed-in account; there is no
/// way to read somebody else's.
/// </summary>
[ApiController]
[Route("api/notifications")]
public class NotificationsController : ControllerBase
{
    private readonly IMediator _mediator;

    public NotificationsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// Returns the caller's notifications, newest first.
    /// </summary>
    [Authorize(Policy = AuthorizationPolicies.RequireUser)]
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<AppNotificationDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetInbox(
        [FromQuery] bool unreadOnly = false,
        [FromQuery] int take = 50,
        CancellationToken cancellationToken = default)
    {
        var response = await _mediator.Send(
            new GetNotificationsQuery(unreadOnly, take),
            cancellationToken);

        return response.ToActionResult(this);
    }

    /// <summary>
    /// Returns how many of the caller's notifications are unread.
    /// </summary>
    [Authorize(Policy = AuthorizationPolicies.RequireUser)]
    [HttpGet("unread-count")]
    [ProducesResponseType(typeof(int), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetUnreadCount(CancellationToken cancellationToken)
    {
        var response = await _mediator.Send(new GetUnreadNotificationCountQuery(), cancellationToken);
        return response.ToActionResult(this);
    }

    /// <summary>
    /// Marks one notification read.
    /// </summary>
    [Authorize(Policy = AuthorizationPolicies.RequireUser)]
    [HttpPut("{notificationKey:guid}/read")]
    [ProducesResponseType(typeof(AppNotificationDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> MarkAsRead(
        Guid notificationKey,
        CancellationToken cancellationToken)
    {
        var response = await _mediator.Send(
            new MarkNotificationAsReadCommand(notificationKey),
            cancellationToken);

        return response.ToActionResult(this);
    }

    /// <summary>
    /// Marks every unread notification read.
    /// </summary>
    [Authorize(Policy = AuthorizationPolicies.RequireUser)]
    [HttpPut("read-all")]
    [ProducesResponseType(typeof(int), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> MarkAllAsRead(CancellationToken cancellationToken)
    {
        var response = await _mediator.Send(new MarkAllNotificationsAsReadCommand(), cancellationToken);
        return response.ToActionResult(this);
    }

    /// <summary>
    /// The VAPID public key a browser subscribes to push with; <c>publicKey</c> is null when this
    /// server sends no pushes (the PWA then hides its switch).
    /// </summary>
    [Authorize(Policy = AuthorizationPolicies.RequireUser)]
    [HttpGet("push/public-key")]
    [ProducesResponseType(typeof(WebPushPublicKeyDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetPushPublicKey(CancellationToken cancellationToken)
    {
        var response = await _mediator.Send(new GetPushPublicKeyQuery(), cancellationToken);
        return response.ToActionResult(this);
    }

    /// <summary>
    /// Registers this device (the browser's <c>PushSubscription.toJSON()</c>) to receive the caller's
    /// notifications as pushes. Posting the same endpoint again refreshes it.
    /// </summary>
    [Authorize(Policy = AuthorizationPolicies.RequireUser)]
    [HttpPost("push/subscriptions")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> SubscribeToPush(
        [FromBody] WebPushSubscriptionRequest request,
        CancellationToken cancellationToken)
    {
        var response = await _mediator.Send(
            new SubscribeToPushCommand(
                request.Endpoint ?? string.Empty,
                request.Keys?.P256dh ?? string.Empty,
                request.Keys?.Auth ?? string.Empty,
                Request.Headers.UserAgent.ToString()),
            cancellationToken);

        return response.ToActionResult(this);
    }

    /// <summary>
    /// Stops pushes to one of the caller's devices: turned off in the app, or signing out on it.
    /// </summary>
    [Authorize(Policy = AuthorizationPolicies.RequireUser)]
    [HttpDelete("push/subscriptions")]
    [ProducesResponseType(typeof(bool), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> UnsubscribeFromPush(
        [FromQuery] string endpoint,
        CancellationToken cancellationToken)
    {
        var response = await _mediator.Send(new UnsubscribeFromPushCommand(endpoint), cancellationToken);
        return response.ToActionResult(this);
    }
}
