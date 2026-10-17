namespace ProjectK.Common.Models.Settings;

/// <summary>
/// The anonymous daily ping to the status service: a random instance id, the release and a few
/// counts, nothing that names a person or a kurin. On in Production and SelfHost; a self-host turns
/// it off with <c>PROJECTK_TELEMETRY=false</c>.
/// </summary>
public sealed class TelemetryOptions
{
    public const string SectionName = "Telemetry";

    /// <summary>
    /// A string, not a bool: the binder throws on <c>0</c>, <c>no</c> or <c>off</c>, and a self-host
    /// that turns the ping off that way must not crash the API at boot. See <see cref="IsEnabled"/>.
    /// </summary>
    public string? Enabled { get; set; }

    /// <summary>Only a clear "on" (<c>true</c>, <c>1</c>, <c>yes</c>, <c>on</c>) pings; anything else, empty included, does not.</summary>
    public bool IsEnabled => IsOn(Enabled);

    public string Endpoint { get; set; } = "https://status.lileyka.app/v1/ping";

    /// <summary>The first ping waits this long after startup, so a crash loop does not ping.</summary>
    public int InitialDelayMinutes { get; set; } = 5;

    public int IntervalHours { get; set; } = 24;

    public int TimeoutSeconds { get; set; } = 15;

    public static bool IsOn(string? value) =>
        value?.Trim().ToLowerInvariant() is "true" or "1" or "yes" or "on";
}
