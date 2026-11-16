namespace ProjectK.API.Hosting;

/// <summary>
/// Optional security-hardening settings, bound from the "SecurityPatch" configuration section.
/// </summary>
public sealed class SecurityPatchOptions
{
    public const string SectionName = "SecurityPatch";

    /// <summary>
    /// Enables geo-blocking of requests originating from <see cref="BlockedCountries"/>.
    /// </summary>
    public bool EnableGeoBlocking { get; set; }

    /// <summary>
    /// ISO country codes to block when <see cref="EnableGeoBlocking"/> is on.
    /// </summary>
    public string[] BlockedCountries { get; set; } = Array.Empty<string>();

    /// <summary>
    /// Request header set by an upstream proxy/CDN (e.g. Cloudflare "CF-IPCountry") carrying the
    /// visitor country. When present it is trusted and no outbound GeoIP call is made.
    /// </summary>
    public string GeoCountryHeader { get; set; } = "CF-IPCountry";
}
