namespace ProjectK.Common.Entities.NotificationsModule;

/// <summary>
/// One device that asked for Web Push: the browser's push service endpoint and the keys the payload
/// is encrypted to (RFC 8291). An account may have several (a phone, a tablet); the endpoint is
/// unique, so the same device signing in as someone else moves the row rather than doubling it.
/// </summary>
public class WebPushSubscription
{
    public Guid WebPushSubscriptionKey { get; set; } = Guid.NewGuid();
    public Guid UserKey { get; set; }

    /// <summary>The push service URL the browser handed out. Opaque; it rotates now and then.</summary>
    public string Endpoint { get; set; } = string.Empty;

    /// <summary>The device's P-256 public key, base64url, as <c>PushSubscription.toJSON().keys.p256dh</c>.</summary>
    public string P256dh { get; set; } = string.Empty;

    /// <summary>The 16-byte auth secret, base64url.</summary>
    public string Auth { get; set; } = string.Empty;

    /// <summary>Which browser asked, for the person to tell devices apart later. Optional.</summary>
    public string? UserAgent { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}
