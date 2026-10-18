namespace ProjectK.Common.Models.Settings;

/// <summary>
/// Web Push (VAPID, RFC 8292). Bound from the <c>WebPush</c> section. Without both keys push is
/// off: the PWA hides its switch and nothing is queued.
/// </summary>
public sealed class WebPushOptions
{
    public const string SectionName = "WebPush";

    /// <summary>Uncompressed P-256 public key, base64url (65 bytes) — what browsers take as applicationServerKey.</summary>
    public string? PublicKey { get; set; }

    /// <summary>The private scalar, base64url (32 bytes). A secret: configuration only, never the repository.</summary>
    public string? PrivateKey { get; set; }

    public const string DefaultSubject = "mailto:projectk-system@mail.lileyka.app";

    /// <summary>Who push services may contact about this sender: a <c>mailto:</c> or <c>https:</c> URL.</summary>
    public string? Subject { get; set; }

    /// <summary><see cref="Subject"/>, or the default when configuration leaves it empty.</summary>
    public string EffectiveSubject => string.IsNullOrWhiteSpace(Subject) ? DefaultSubject : Subject;

    public bool IsConfigured => !string.IsNullOrWhiteSpace(PublicKey) && !string.IsNullOrWhiteSpace(PrivateKey);
}
