namespace ProjectK.Common.Contracts.Shared;

/// <summary>
/// The country a request comes from, for the geo-block in front of every request. A proxy's own answer
/// (Cloudflare's <c>CF-IPCountry</c>) is taken as is; otherwise the address is looked up outside.
/// </summary>
public interface IGeoIPService
{
    /// <summary>
    /// The two-letter country code; <c>"LOCAL"</c> for an address that cannot be placed (loopback, LAN,
    /// containers), and null when the lookup gave no answer.
    /// </summary>
    Task<string?> GetCountryCodeAsync(string? forwardedCountry, string ip);
}
