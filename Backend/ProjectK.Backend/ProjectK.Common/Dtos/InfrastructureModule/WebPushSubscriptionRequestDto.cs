namespace ProjectK.Common.Dtos.InfrastructureModule;

/// <summary>A browser's <c>PushSubscription.toJSON()</c>, as the PWA posts it.</summary>
public sealed class WebPushSubscriptionRequestDto
{
    public string? Endpoint { get; set; }
    public WebPushKeysDto? Keys { get; set; }
}
