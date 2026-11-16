namespace ProjectK.Common.Dtos.InfrastructureModule.Requests;

/// <summary>A browser's <c>PushSubscription.toJSON()</c>, as the PWA posts it.</summary>
public sealed class WebPushSubscriptionRequest
{
    public string? Endpoint { get; set; }
    public WebPushKeysDto? Keys { get; set; }
}
