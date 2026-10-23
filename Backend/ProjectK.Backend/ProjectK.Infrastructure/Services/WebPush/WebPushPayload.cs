using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using ProjectK.Common.Models.Dtos.InfrastructureModule;

namespace ProjectK.Infrastructure.Services.WebPush;

/// <summary>
/// What the device receives: the Angular service worker's notification message. ngsw shows
/// <c>notification</c> as is and, on a tap, runs <c>data.onActionClick.default</c> — here it opens
/// the screen (or brings the open app there). Kept small: push services cap the body at 4 KB.
/// </summary>
public static class WebPushPayload
{
    public const string Icon = PushRoutes.Base + "icons/icon-192.png";
    private const int MaxTitle = 120;
    private const int MaxBody = 500;

    private static readonly JsonSerializerOptions Json = new()
    {
        // Cyrillic as UTF-8, not \uXXXX: six bytes a letter would eat the 4 KB in a paragraph.
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public static byte[] Build(AppNotificationDto notification, string url)
    {
        var payload = Build(notification, url, MaxBody);
        // Only a body in an unusual alphabet can still overflow; the screen it opens has it all.
        return payload.Length <= WebPushCrypto.MaxPayloadSize ? payload : Build(notification, url, 80);
    }

    private static byte[] Build(AppNotificationDto notification, string url, int maxBody)
    {
        var message = new Message(new Shown(
            Cut(notification.Title, MaxTitle),
            Cut(notification.Body, maxBody),
            Icon,
            // The OS replaces a shown notification with the same tag instead of stacking another.
            notification.DeduplicationKey ?? notification.Type.ToString(),
            new Data(
                notification.NotificationKey,
                new Actions(new OnClick("navigateLastFocusedOrOpen", url)))));

        return JsonSerializer.SerializeToUtf8Bytes(message, Json);
    }

    private static string Cut(string text, int max) => text.Length <= max ? text : text[..(max - 1)] + "…";

    private sealed record Message([property: JsonPropertyName("notification")] Shown Notification);

    private sealed record Shown(
        [property: JsonPropertyName("title")] string Title,
        [property: JsonPropertyName("body")] string Body,
        [property: JsonPropertyName("icon")] string IconUrl,
        [property: JsonPropertyName("tag")] string Tag,
        [property: JsonPropertyName("data")] Data Data);

    private sealed record Data(
        [property: JsonPropertyName("notificationKey")] Guid NotificationKey,
        [property: JsonPropertyName("onActionClick")] Actions OnActionClick);

    private sealed record Actions([property: JsonPropertyName("default")] OnClick Default);

    private sealed record OnClick(
        [property: JsonPropertyName("operation")] string Operation,
        [property: JsonPropertyName("url")] string Url);
}
