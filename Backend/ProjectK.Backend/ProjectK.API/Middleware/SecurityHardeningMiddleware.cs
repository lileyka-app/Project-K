using Microsoft.Extensions.Options;
using ProjectK.API.Hosting;
using ProjectK.Common.Contracts.InfrastructureModule;

namespace ProjectK.API.Middleware;

public sealed class SecurityHardeningMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IOptions<SecurityPatchOptions> _options;

    public SecurityHardeningMiddleware(RequestDelegate next, IOptions<SecurityPatchOptions> options)
    {
        _next = next;
        _options = options;
    }

    public async Task InvokeAsync(HttpContext context, IGeoIPService geoIPService, IActivityLogger activityLogger)
    {
        var remoteIp = context.Connection.RemoteIpAddress?.ToString();

        if (remoteIp == null)
        {
            await _next(context);
            return;
        }

        // 1. Geo-blocking (RU/BY check)
        if (_options.Value.EnableGeoBlocking)
        {
            var headerName = _options.Value.GeoCountryHeader;
            var forwardedCountry = context.Request.Headers[headerName].ToString();

            var countryCode = await geoIPService.GetCountryCodeAsync(forwardedCountry, remoteIp);
            if (countryCode != null && _options.Value.BlockedCountries.Contains(countryCode, StringComparer.OrdinalIgnoreCase))
            {
                activityLogger.ReportGeoBlocked(remoteIp, countryCode);
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                await context.Response.WriteAsJsonAsync(new
                {
                    error = "RegionRestricted",
                    message = "Access from your region is restricted."
                }, context.RequestAborted);
                return;
            }
        }

        await _next(context);
    }
}
