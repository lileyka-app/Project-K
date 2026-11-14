namespace ProjectK.Common.Dtos.InfrastructureModule;

/// <summary>The server's VAPID public key (base64url), or <c>null</c> when push is not set up here.</summary>
public sealed class WebPushPublicKeyDto
{
    public string? PublicKey { get; set; }
}
