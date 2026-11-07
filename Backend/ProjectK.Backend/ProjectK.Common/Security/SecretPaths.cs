namespace ProjectK.Common.Security;

/// <summary>
/// Paths whose tail is a secret. The calendar link carries its token in the path, because a calendar
/// app sends nothing else, and so does a shared member card; every place that writes a request path
/// down — the request log, the scope of every log line inside the request, the activity record,
/// telemetry — cuts it here first.
/// </summary>
public static class SecretPaths
{
    public const string CalendarFeedPrefix = "/api/calendar/feed/";
    public const string PublicCardPrefix = "/api/public/card/";
    private const string Masked = "***";

    private static readonly string[] Prefixes = [CalendarFeedPrefix, PublicCardPrefix];

    public static bool IsSecret(string? path) => PrefixOf(path) is not null;

    public static string? Mask(string? path) => PrefixOf(path) is { } prefix ? prefix + Masked : path;

    /// <summary>The same for a full URL (telemetry keeps the whole of it).</summary>
    public static string? MaskUrl(string? url)
    {
        if (url is null)
        {
            return null;
        }

        foreach (var prefix in Prefixes)
        {
            var at = url.IndexOf(prefix, StringComparison.OrdinalIgnoreCase);
            if (at >= 0)
            {
                return url[..(at + prefix.Length)] + Masked;
            }
        }

        return url;
    }

    private static string? PrefixOf(string? path) =>
        path is null ? null : Prefixes.FirstOrDefault(p => path.StartsWith(p, StringComparison.OrdinalIgnoreCase));
}
