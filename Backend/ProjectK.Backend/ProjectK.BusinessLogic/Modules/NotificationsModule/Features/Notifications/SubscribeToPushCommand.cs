using FluentValidation;
using MediatR;
using Microsoft.Extensions.Options;
using ProjectK.Common.Contracts.Shared;
using ProjectK.Common.Entities.NotificationsModule;
using ProjectK.Common.Enums.Shared;
using ProjectK.Common.Options.NotificationsModule;
using ProjectK.Common.Records.Shared;

namespace ProjectK.BusinessLogic.Modules.NotificationsModule.Features.Notifications;

/// <summary>
/// Registers the caller's device for push, or refreshes it: the endpoint is the identity, so the
/// same device posting again (on every app start, as endpoints rotate) updates its row.
/// </summary>
public sealed record SubscribeToPushCommand(string Endpoint, string P256dh, string Auth, string? UserAgent)
    : IRequest<ServiceResult<Unit>>;

public sealed class SubscribeToPushCommandHandler
    : IRequestHandler<SubscribeToPushCommand, ServiceResult<Unit>>
{
    private const int MaxUserAgentLength = 300;

    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserContext _currentUserContext;
    private readonly WebPushOptions _options;

    public SubscribeToPushCommandHandler(
        IUnitOfWork unitOfWork,
        ICurrentUserContext currentUserContext,
        IOptions<WebPushOptions> options)
    {
        _unitOfWork = unitOfWork;
        _currentUserContext = currentUserContext;
        _options = options.Value;
    }

    public async Task<ServiceResult<Unit>> Handle(SubscribeToPushCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUserContext.UserId.HasValue)
        {
            return ServiceResult<Unit>.Failure(ResultType.Unauthorized, NotificationsErrors.Unauthorized, "No one is signed in.");
        }

        if (!_options.IsConfigured)
        {
            return ServiceResult<Unit>.Failure(ResultType.BadRequest, NotificationsErrors.PushNotConfigured, "This server does not send push notifications.");
        }

        var userKey = _currentUserContext.UserId.Value;
        var now = DateTime.UtcNow;
        var userAgent = string.IsNullOrWhiteSpace(request.UserAgent)
            ? null
            : request.UserAgent[..Math.Min(request.UserAgent.Length, MaxUserAgentLength)];

        var device = await _unitOfWork.WebPushSubscriptions.GetByEndpointAsync(request.Endpoint, cancellationToken);
        if (device is null)
        {
            _unitOfWork.WebPushSubscriptions.Create(new WebPushSubscription
            {
                UserKey = userKey,
                Endpoint = request.Endpoint,
                P256dh = request.P256dh,
                Auth = request.Auth,
                UserAgent = userAgent,
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            }, cancellationToken);
        }
        else
        {
            // The same device, maybe signed in as someone else now: it follows whoever asked last.
            device.UserKey = userKey;
            device.P256dh = request.P256dh;
            device.Auth = request.Auth;
            device.UserAgent = userAgent ?? device.UserAgent;
            device.UpdatedAtUtc = now;
            _unitOfWork.WebPushSubscriptions.Update(device, cancellationToken);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return new ServiceResult<Unit>(ResultType.Success, Unit.Value);
    }
}

/// <summary>
/// The server will POST to the endpoint later, so it must be a real push service: https on one of
/// the hosts browsers use, never an address of the caller's choosing.
/// </summary>
public sealed class SubscribeToPushCommandValidator : AbstractValidator<SubscribeToPushCommand>
{
    public const int MaxEndpointLength = 800;

    /// <summary>
    /// Chrome, Edge, Samsung and Opera on Android (FCM), Firefox (Mozilla autopush), Safari on iPhone
    /// and Mac (Apple), Edge on Windows (WNS).
    /// </summary>
    private static readonly string[] PushServiceHosts =
    [
        "fcm.googleapis.com",
        "android.googleapis.com",
        "push.services.mozilla.com",
        "push.apple.com",
        "notify.windows.com",
    ];

    public SubscribeToPushCommandValidator()
    {
        RuleFor(command => command.Endpoint)
            .NotEmpty()
            .MaximumLength(MaxEndpointLength)
            .Must(IsPushService)
            .WithMessage("The endpoint is not a known push service.");

        // A P-256 point is 65 bytes (87 characters in base64url), the auth secret 16 (22).
        RuleFor(command => command.P256dh)
            .NotEmpty()
            .MaximumLength(200);
        RuleFor(command => command.Auth)
            .NotEmpty()
            .MaximumLength(100);
    }

    public static bool IsPushService(string? endpoint)
    {
        if (!Uri.TryCreate(endpoint, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps || !uri.IsDefaultPort)
        {
            return false;
        }

        var host = uri.IdnHost;
        return PushServiceHosts.Any(allowed =>
            host.Equals(allowed, StringComparison.OrdinalIgnoreCase)
            || host.EndsWith("." + allowed, StringComparison.OrdinalIgnoreCase));
    }
}
