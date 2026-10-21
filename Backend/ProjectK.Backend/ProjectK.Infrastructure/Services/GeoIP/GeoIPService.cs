using System.Net;
using System.Net.Http.Json;
using System.Net.Sockets;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace ProjectK.Infrastructure.Services.GeoIP;

public sealed class GeoIPService
{
    // The lookup sits in SecurityHardeningMiddleware, so every request pays for it.
    // Cap it well below the 100s HttpClient default: a slow or throttled provider must
    // never hold a request open.
    private static readonly TimeSpan LookupTimeout = TimeSpan.FromSeconds(3);
    // ip-api.com allows 45 requests/min and answers with 429 beyond that. Without a
    // negative entry every subsequent request retries and stalls again.
    private static readonly TimeSpan FailureCacheDuration = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan SuccessCacheDuration = TimeSpan.FromDays(1);

    private readonly HttpClient _httpClient;
    private readonly IMemoryCache _cache;
    private readonly ILogger<GeoIPService> _logger;

    public GeoIPService(HttpClient httpClient, IMemoryCache cache, ILogger<GeoIPService> logger)
    {
        _httpClient = httpClient;
        _cache = cache;
        _logger = logger;
    }

    public Task<string?> GetCountryCodeAsync(string ip)
        => GetCountryCodeAsync(null, ip);

    public async Task<string?> GetCountryCodeAsync(string? forwardedCountry, string ip)
    {
        // A proxy/CDN in front (e.g. Cloudflare's CF-IPCountry) already resolved the country,
        // so trust it and skip the outbound call entirely. Cloudflare sends "XX" for unknown
        // and "T1" for Tor — treat those as no answer and fall through to the IP lookup.
        if (IsUsableCountryCode(forwardedCountry))
            return forwardedCountry!.ToUpperInvariant();

        if (string.IsNullOrEmpty(ip))
            return "LOCAL";

        // Containers, LAN clients and reverse proxies present a non-routable source
        // address. It can never be geolocated, so calling out is pure latency — and in
        // Docker that is every single request.
        if (!IPAddress.TryParse(ip, out var address) || IsNonRoutable(address))
            return "LOCAL";

        if (_cache.TryGetValue(ip, out string? cachedCountry))
            return cachedCountry;

        try
        {
            using var timeout = new CancellationTokenSource(LookupTimeout);

            // Using ip-api.com (free for non-commercial use, 45 requests/min)
            var response = await _httpClient.GetFromJsonAsync<IpApiResponse>(
                $"http://ip-api.com/json/{ip}?fields=status,countryCode",
                timeout.Token);

            if (response?.status == "success" && !string.IsNullOrEmpty(response.countryCode))
            {
                _cache.Set(ip, response.countryCode, SuccessCacheDuration);
                return response.countryCode;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching GeoIP data for {IP}", ip);
        }

        // Unknown country is not blocked upstream, so caching the miss only trades a
        // repeated outbound call for a few minutes of staleness.
        _cache.Set(ip, (string?)null, FailureCacheDuration);
        return null;
    }

    private static bool IsUsableCountryCode(string? code)
        => !string.IsNullOrWhiteSpace(code)
           && code.Length == 2
           && !code.Equals("XX", StringComparison.OrdinalIgnoreCase)
           && !code.Equals("T1", StringComparison.OrdinalIgnoreCase);

    private static bool IsNonRoutable(IPAddress address)
    {
        if (IPAddress.IsLoopback(address))
            return true;

        if (address.IsIPv4MappedToIPv6)
            address = address.MapToIPv4();

        if (address.AddressFamily == AddressFamily.InterNetworkV6)
            return address.IsIPv6LinkLocal || address.IsIPv6SiteLocal || address.IsIPv6UniqueLocal;

        var octets = address.GetAddressBytes();
        return octets[0] switch
        {
            10 => true,
            127 => true,
            169 => octets[1] == 254,                    // link-local
            172 => octets[1] >= 16 && octets[1] <= 31,  // 172.16.0.0/12, the Docker range
            192 => octets[1] == 168,
            100 => octets[1] >= 64 && octets[1] <= 127, // CGNAT, used by Tailscale
            _ => false
        };
    }

    private sealed record IpApiResponse(string status, string countryCode);
}
