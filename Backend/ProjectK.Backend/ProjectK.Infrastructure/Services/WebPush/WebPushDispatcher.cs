using System.Security.Cryptography;
using Microsoft.Extensions.Logging;
using ProjectK.Common.Contracts.Shared;
using ProjectK.Common.Dtos.InfrastructureModule;
using ProjectK.Common.Entities.InfrastructureModule;

namespace ProjectK.Infrastructure.Services.WebPush;

/// <summary>
/// Sends one notification to every device of its recipient and forgets the devices the push service
/// no longer knows (404/410) — a phone that dropped the app or was reset. Other failures are logged
/// and left: the next notification tries again, with no retry loop of its own.
/// </summary>
public sealed class WebPushDispatcher
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly WebPushSender _sender;
    private readonly ILogger<WebPushDispatcher> _logger;

    public WebPushDispatcher(IUnitOfWork unitOfWork, WebPushSender sender, ILogger<WebPushDispatcher> logger)
    {
        _unitOfWork = unitOfWork;
        _sender = sender;
        _logger = logger;
    }

    public async Task DeliverAsync(AppNotificationDto notification, CancellationToken cancellationToken)
    {
        var devices = await _unitOfWork.WebPushSubscriptions.GetByUserAsync(notification.RecipientUserKey, cancellationToken);
        if (devices.Count == 0)
        {
            return;
        }

        var activeKurin = PushRoutes.NeedsActiveKurin(notification)
            ? await _unitOfWork.Users.GetActiveKurinKeyAsync(notification.RecipientUserKey, cancellationToken)
            : null;
        var payload = WebPushPayload.Build(notification, PushRoutes.For(notification, activeKurin));

        var gone = 0;
        foreach (var device in devices)
        {
            var outcome = await SendAsync(device, payload, cancellationToken);
            if (outcome == WebPushOutcome.Gone)
            {
                _unitOfWork.WebPushSubscriptions.Delete(device, cancellationToken);
                gone++;
            }
        }

        if (gone > 0)
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("Web push: forgot {Count} device(s) the push service no longer knows.", gone);
        }
    }

    private async Task<WebPushOutcome> SendAsync(
        WebPushSubscription device,
        byte[] payload,
        CancellationToken cancellationToken)
    {
        try
        {
            var outcome = await _sender.SendAsync(device, payload, cancellationToken);
            if (outcome == WebPushOutcome.Failed)
            {
                _logger.LogWarning("Web push to {Host} was refused.", HostOf(device.Endpoint));
            }

            return outcome;
        }
        catch (Exception ex) when (ex is FormatException or ArgumentException or CryptographicException)
        {
            // Keys that cannot be read will never work; the device subscribes again when it opens the app.
            _logger.LogWarning(ex, "Web push: the device's keys are unusable; forgetting it.");
            return WebPushOutcome.Gone;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException && !cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning(ex, "Web push to {Host} did not go through.", HostOf(device.Endpoint));
            return WebPushOutcome.Failed;
        }
    }

    private static string HostOf(string endpoint) =>
        Uri.TryCreate(endpoint, UriKind.Absolute, out var uri) ? uri.Host : "?";
}
