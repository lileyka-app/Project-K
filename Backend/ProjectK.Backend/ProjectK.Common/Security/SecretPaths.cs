namespace ProjectK.Common.Security;

/// <summary>
/// Paths whose tail is a secret. The calendar link carries its token in the path, because a calendar
/// app sends nothing else; every place that writes a request path down — the request log, the scope
/// of every log line inside the request, the activity record, telemetry — cuts it here first.
/// </summary>
public static class SecretPaths
{
    public const string CalendarFeedPrefix = "/api/calendar/feed/";
    private const string Masked = "***";

    public static bool IsSecret(string? path) =>
        path is not null && path.StartsWith(CalendarFeedPrefix, StringComparison.OrdinalIgnoreCase);

    public static string? Mask(string? path) => IsSecret(path) ? CalendarFeedPrefix + Masked : path;

    /// <summary>The same for a full URL (telemetry keeps the whole of it).</summary>
    public static string? MaskUrl(string? url)
    {
        if (url is null)
        {
            return null;
        }

        var at = url.IndexOf(CalendarFeedPrefix, StringComparison.OrdinalIgnoreCase);
        return at < 0 ? url : url[..(at + CalendarFeedPrefix.Length)] + Masked;
    }
}
