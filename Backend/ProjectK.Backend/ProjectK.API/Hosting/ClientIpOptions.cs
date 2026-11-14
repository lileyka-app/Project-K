using System.Net;
using Microsoft.AspNetCore.HttpOverrides;

namespace ProjectK.API.Hosting;

/// <summary>
/// Where the visitor's address is read from, bound from <c>Security:ClientIp</c>. The address is what
/// the sign-in rate limiter partitions on, what the geo-block looks up and what the security log
/// records, so it has to come from a source the caller cannot write.
/// </summary>
public sealed class ClientIpOptions
{
    public const string SectionName = "Security:ClientIp";

    /// <summary>
    /// A request header that carries the visitor's address and is set by the only proxy that can
    /// reach this API — Cloudflare's <c>CF-Connecting-IP</c> in Azure once App Service admits
    /// Cloudflare alone, nginx's <c>X-Real-IP</c> in the self-host bundle where the API port is not
    /// published. When set, its value replaces the connection address for the rest of the pipeline.
    /// Empty means the connection address, after <c>X-Forwarded-For</c> from <see cref="TrustedProxies"/>.
    /// </summary>
    public string? Header { get; set; }

    /// <summary>
    /// Proxies whose <c>X-Forwarded-For</c> is believed, as addresses (<c>10.0.0.4</c>) or networks
    /// (<c>10.0.0.0/8</c>). Empty keeps the framework's list empty and the header accepted from any
    /// address — the App Service front end has no fixed address to name, which is why
    /// <see cref="Header"/> exists.
    /// </summary>
    public string[] TrustedProxies { get; set; } = [];

    /// <summary>
    /// Adds <see cref="TrustedProxies"/> to the forwarded-headers options. An entry that parses as
    /// neither an address nor a network is skipped and named in the returned list so startup can say so.
    /// </summary>
    public IReadOnlyList<string> ApplyTo(ForwardedHeadersOptions options)
    {
        var rejected = new List<string>();

        foreach (var entry in TrustedProxies)
        {
            var trimmed = entry?.Trim();
            if (string.IsNullOrEmpty(trimmed))
            {
                continue;
            }

            if (IPAddress.TryParse(trimmed, out var address))
            {
                options.KnownProxies.Add(address);
                continue;
            }

            if (System.Net.IPNetwork.TryParse(trimmed, out var network))
            {
                options.KnownIPNetworks.Add(network);
                continue;
            }

            rejected.Add(trimmed);
        }

        return rejected;
    }
}
