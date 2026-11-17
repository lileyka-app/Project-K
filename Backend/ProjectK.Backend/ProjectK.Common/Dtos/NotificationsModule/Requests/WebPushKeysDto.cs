namespace ProjectK.Common.Dtos.NotificationsModule.Requests;

public sealed class WebPushKeysDto
{
    public string? P256dh { get; set; }
    public string? Auth { get; set; }
}
