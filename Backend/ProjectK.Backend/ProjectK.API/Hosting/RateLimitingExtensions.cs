using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using ProjectK.Common.Contracts.NotificationsModule;
using ProjectK.Common.Rules.IdentityModule;
using ProjectK.Common.Rules.Shared;

namespace ProjectK.API.Hosting;

/// <summary>
/// The request limits: a global one per person, address or calendar link, and the named policies
/// endpoints opt into (<c>StrictAuthLimit</c>, <c>AccountSecurityLimit</c>, <c>CalendarFeedLimit</c>,
/// <c>PublicCardLimit</c>). A request carrying the configured <c>X-RateLimit-Bypass</c> key skips all
/// but the calendar and card limits.
/// </summary>
public static class RateLimitingExtensions
{
    /// <summary>Registers the limiter with the global limit and every named policy.</summary>
    public static IServiceCollection AddLileykaRateLimiting(this IServiceCollection services, IConfiguration configuration)
    {
        // The strict limits are per address, and on a container stand every browser arrives from
        // the same docker gateway address: five failed sign-ins by anyone on the tailnet locked
        // everyone out for five minutes. Production sees real addresses (Security:ClientIp), but a
        // kurin signing in together from one school or home Wi-Fi is one address too, so the default
        // leaves room for a group; guessing one account's password is stopped per email by
        // LoginThrottle instead. The local tiers raise the limit further in their appsettings.
        var strictPermit = configuration.GetValue("RateLimiting:StrictAuth:PermitLimit", 20);
        var strictWindow = TimeSpan.FromMinutes(configuration.GetValue("RateLimiting:StrictAuth:WindowMinutes", 5));
        var accountPermit = configuration.GetValue("RateLimiting:AccountSecurity:PermitLimit", 10);
        var accountWindow = TimeSpan.FromMinutes(configuration.GetValue("RateLimiting:AccountSecurity:WindowMinutes", 5));
        // Apple polls a subscription as often as every 5 minutes; Google and Outlook far less.
        var calendarFeedPermit = configuration.GetValue("RateLimiting:CalendarFeed:PermitLimit", 60);
        var calendarFeedWindow = TimeSpan.FromMinutes(configuration.GetValue("RateLimiting:CalendarFeed:WindowMinutes", 60));

        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
            {
                var bypassPartition = TryGetBypassPartition(httpContext, configuration);
                if (bypassPartition is not null)
                {
                    return bypassPartition.Value;
                }

                return RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: CalendarFeedPartition(httpContext)
                        ?? httpContext.User.GetUserKeyValue()
                        ?? httpContext.Connection.RemoteIpAddress?.ToString()
                        ?? httpContext.Request.Headers.Host.ToString(),
                    factory: partition => new FixedWindowRateLimiterOptions
                    {
                        AutoReplenishment = true,
                        PermitLimit = 300,
                        QueueLimit = 0,
                        Window = TimeSpan.FromMinutes(1)
                    });
            });

            options.AddPolicy<string>("StrictAuthLimit", httpContext =>
            {
                var bypassPartition = TryGetBypassPartition(httpContext, configuration);
                if (bypassPartition is not null)
                {
                    return bypassPartition.Value;
                }

                return RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "anonymous",
                    factory: partition => new FixedWindowRateLimiterOptions
                    {
                        AutoReplenishment = true,
                        PermitLimit = strictPermit,
                        QueueLimit = 0,
                        Window = strictWindow
                    });
            });

            options.AddPolicy<string>("AccountSecurityLimit", httpContext =>
            {
                var bypassPartition = TryGetBypassPartition(httpContext, configuration);
                if (bypassPartition is not null)
                {
                    return bypassPartition.Value;
                }

                var partitionKey = httpContext.User.GetUserKeyValue()
                    ?? httpContext.Connection.RemoteIpAddress?.ToString()
                    ?? "anonymous";

                return RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: partitionKey,
                    factory: partition => new FixedWindowRateLimiterOptions
                    {
                        AutoReplenishment = true,
                        PermitLimit = accountPermit,
                        QueueLimit = 0,
                        Window = accountWindow
                    });
            });

            options.AddPolicy<string>("CalendarFeedLimit", httpContext =>
                RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: CalendarFeedPartition(httpContext) ?? "calendar-feed",
                    factory: partition => new FixedWindowRateLimiterOptions
                    {
                        AutoReplenishment = true,
                        PermitLimit = calendarFeedPermit,
                        QueueLimit = 0,
                        Window = calendarFeedWindow
                    }));

            // A card is opened by a person who scanned it, a few times at most; anything faster is guessing.
            options.AddPolicy<string>("PublicCardLimit", httpContext =>
                RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "public-card",
                    factory: partition => new FixedWindowRateLimiterOptions
                    {
                        AutoReplenishment = true,
                        PermitLimit = 30,
                        QueueLimit = 0,
                        Window = TimeSpan.FromMinutes(1)
                    }));

            options.OnRejected = async (context, token) =>
            {
                var endpoint = context.HttpContext.GetEndpoint();
                // No endpoint policy means the global limiter turned it away — said so, rather than left empty.
                var policyName = endpoint?.Metadata.GetMetadata<EnableRateLimitingAttribute>()?.PolicyName ?? "global";
                var activityLogger = context.HttpContext.RequestServices.GetService<IActivityLogger>();
                activityLogger?.ReportRateLimitRejection(policyName);
                await context.HttpContext.Response.WriteAsync("Too many requests. Please try again later.", token);
            };
        });

        return services;
    }

    /// <summary>Puts the limiter into the pipeline; the policies are those <see cref="AddLileykaRateLimiting"/> registered.</summary>
    public static IApplicationBuilder UseLileykaRateLimiting(this IApplicationBuilder app) => app.UseRateLimiter();

    /// <summary>
    /// Calendar links are counted per link, not per address: Google and Outlook fetch every
    /// subscriber's link from the same few data-centre addresses, which an address limit would choke.
    /// </summary>
    private static string? CalendarFeedPartition(HttpContext httpContext)
    {
        var path = httpContext.Request.Path.Value;
        return SecretPathRules.IsSecret(path) ? "calendar-feed:" + SecretHashRules.Sha256Hex(path!)[..32] : null;
    }

    private static RateLimitPartition<string>? TryGetBypassPartition(HttpContext httpContext, IConfiguration configuration)
    {
        var bypassKey = configuration["RateLimitBypassKey"];
        if (!string.IsNullOrEmpty(bypassKey) &&
            httpContext.Request.Headers.TryGetValue("X-RateLimit-Bypass", out var providedKey) &&
            SecretComparisonRules.Matches(providedKey.ToString(), bypassKey))
        {
            return RateLimitPartition.GetNoLimiter("Bypass");
        }

        return null;
    }
}
