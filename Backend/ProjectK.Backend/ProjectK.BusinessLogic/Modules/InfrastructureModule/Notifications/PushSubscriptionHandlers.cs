using FluentValidation;
using MediatR;
using Microsoft.Extensions.Options;
using ProjectK.Common.Entities.InfrastructureModule;
using ProjectK.Common.Interfaces;
using ProjectK.Common.Interfaces.Modules.InfrastructureModule;
using ProjectK.Common.Models.Dtos.InfrastructureModule;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Records;
using ProjectK.Common.Models.Settings;

namespace ProjectK.BusinessLogic.Modules.InfrastructureModule.Notifications;

/// <summary>The key a browser subscribes with; <c>null</c> when this server sends no pushes.</summary>
public sealed record GetPushPublicKeyQuery : IRequest<ServiceResult<WebPushPublicKeyDto>>;

/// <summary>
/// Registers the caller's device for push, or refreshes it: the endpoint is the identity, so the
/// same device posting again (on every app start, as endpoints rotate) updates its row.
/// </summary>
public sealed record SubscribeToPushCommand(string Endpoint, string P256dh, string Auth, string? UserAgent)
    : IRequest<ServiceResult<bool>>;

/// <summary>Forgets one of the caller's devices. Idempotent: an unknown endpoint is already forgotten.</summary>
public sealed record UnsubscribeFromPushCommand(string Endpoint) : IRequest<ServiceResult<bool>>;

public sealed class GetPushPublicKeyQueryHandler
    : IRequestHandler<GetPushPublicKeyQuery, ServiceResult<WebPushPublicKeyDto>>
{
    private readonly WebPushOptions _options;

    public GetPushPublicKeyQueryHandler(IOptions<WebPushOptions> options)
    {
        _options = options.Value;
    }

    public Task<ServiceResult<WebPushPublicKeyDto>> Handle(GetPushPublicKeyQuery request, CancellationToken cancellationToken)
    {
        var key = new WebPushPublicKeyDto { PublicKey = _options.IsConfigured ? _options.PublicKey!.Trim() : null };
        return Task.FromResult(new ServiceResult<WebPushPublicKeyDto>(ResultType.Success, key));
    }
}

public sealed class SubscribeToPushCommandHandler
    : IRequestHandler<SubscribeToPushCommand, ServiceResult<bool>>
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

    public async Task<ServiceResult<bool>> Handle(SubscribeToPushCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUserContext.UserId.HasValue)
        {
            return new ServiceResult<bool>(ResultType.Unauthorized);
        }

        if (!_options.IsConfigured)
        {
            return ServiceResult<bool>.Failure(ResultType.BadRequest, "PushNotConfigured", "This server does not send push notifications.");
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
        return new ServiceResult<bool>(ResultType.Success, true);
    }
}

public sealed class UnsubscribeFromPushCommandHandler
    : IRequestHandler<UnsubscribeFromPushCommand, ServiceResult<bool>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserContext _currentUserContext;

    public UnsubscribeFromPushCommandHandler(IUnitOfWork unitOfWork, ICurrentUserContext currentUserContext)
    {
        _unitOfWork = unitOfWork;
        _currentUserContext = currentUserContext;
    }

    public async Task<ServiceResult<bool>> Handle(UnsubscribeFromPushCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUserContext.UserId.HasValue)
        {
            return new ServiceResult<bool>(ResultType.Unauthorized);
        }

        var device = await _unitOfWork.WebPushSubscriptions.GetByEndpointAsync(request.Endpoint, cancellationToken);
        // Somebody else's device is not the caller's to forget; the answer is the same either way.
        if (device is null || device.UserKey != _currentUserContext.UserId.Value)
        {
            return new ServiceResult<bool>(ResultType.Success, false);
        }

        _unitOfWork.WebPushSubscriptions.Delete(device, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return new ServiceResult<bool>(ResultType.Success, true);
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
