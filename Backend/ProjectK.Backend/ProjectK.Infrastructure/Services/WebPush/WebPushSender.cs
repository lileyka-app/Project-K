using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using Microsoft.Extensions.Options;
using ProjectK.Common.Entities.InfrastructureModule;
using ProjectK.Common.Options.InfrastructureModule;

namespace ProjectK.Infrastructure.Services.WebPush;

/// <summary>What became of one push.</summary>
public enum WebPushOutcome
{
    Delivered,

    /// <summary>The push service no longer knows the device (404/410): its row should go.</summary>
    Gone,

    /// <summary>Anything else — refused, throttled, unreachable. Logged; the row stays.</summary>
    Failed
}

/// <summary>
/// Sends one encrypted message to one device's push service (RFC 8030), signed with this server's
/// VAPID key. A typed HttpClient, so the handler is pooled and a test can put its own behind it.
/// </summary>
public sealed class WebPushSender
{
    /// <summary>How long the push service keeps a message for a phone that is off: a day.</summary>
    private const int TimeToLiveSeconds = 86_400;

    private static readonly TimeSpan VapidLifetime = TimeSpan.FromHours(12);

    private readonly HttpClient _http;
    private readonly WebPushOptions _options;
    private readonly TimeProvider _time;

    public WebPushSender(HttpClient http, IOptions<WebPushOptions> options, TimeProvider time)
    {
        _http = http;
        _options = options.Value;
        _time = time;
    }

    public async Task<WebPushOutcome> SendAsync(
        WebPushSubscription subscription,
        byte[] payload,
        CancellationToken cancellationToken)
    {
        if (!Uri.TryCreate(subscription.Endpoint, UriKind.Absolute, out var endpoint))
        {
            return WebPushOutcome.Gone;
        }

        // The server's own key first: a misconfigured pair must fail loudly, not pass for a dead device.
        var authorization = VapidAuthorization(endpoint);
        var content = new ByteArrayContent(WebPushCryptoRules.Encrypt(payload, subscription.P256dh, subscription.Auth));
        content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        content.Headers.ContentEncoding.Add("aes128gcm");
        using var request = new HttpRequestMessage(HttpMethod.Post, endpoint) { Content = content };
        request.Headers.TryAddWithoutValidation("TTL", TimeToLiveSeconds.ToString(System.Globalization.CultureInfo.InvariantCulture));
        request.Headers.TryAddWithoutValidation("Urgency", "normal");
        request.Headers.TryAddWithoutValidation("Authorization", authorization);

        using var response = await _http.SendAsync(request, cancellationToken);
        if (response.IsSuccessStatusCode)
        {
            return WebPushOutcome.Delivered;
        }

        return response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Gone
            ? WebPushOutcome.Gone
            : WebPushOutcome.Failed;
    }

    private string VapidAuthorization(Uri endpoint)
    {
        try
        {
            return WebPushCryptoRules.CreateVapidAuthorization(
                endpoint,
                _options.EffectiveSubject,
                _options.PublicKey!,
                _options.PrivateKey!,
                _time.GetUtcNow().Add(VapidLifetime));
        }
        catch (Exception ex) when (ex is FormatException or ArgumentException or CryptographicException)
        {
            throw new InvalidOperationException("WebPush:PublicKey and WebPush:PrivateKey are not a usable VAPID key pair.", ex);
        }
    }
}
