using System;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ProjectK.Common.Extensions;
using ProjectK.Common.Interfaces.Modules.InfrastructureModule;
using ProjectK.Common.Models.Settings;
using ProjectK.Common.Security;
using Serilog.Context;

namespace ProjectK.Infrastructure.Logging;

public sealed class ActivityLogger : IActivityLogger
{
    private readonly ILogger<ActivityLogger> _logger;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IMemoryCache _cache;
    private readonly SecurityMonitoringOptions _options;
    private readonly object _cacheLock = new();

    public ActivityLogger(
        ILogger<ActivityLogger> logger,
        IHttpContextAccessor httpContextAccessor,
        IMemoryCache cache,
        IOptions<SecurityMonitoringOptions> options)
    {
        _logger = logger;
        _httpContextAccessor = httpContextAccessor;
        _cache = cache;
        _options = options.Value;
    }

    public void LogAudit(
        string action,
        Guid? actorUserId = null,
        Guid? targetUserId = null,
        string? email = null,
        string? newEmail = null,
        string? reason = null)
    {
        var context = BuildContext();
        var actorId = actorUserId?.ToString() ?? context.UserId;

        using (LogContext.PushProperty("EventType", "Audit.Action"))
        using (LogContext.PushProperty("Action", action))
        {
            var payload = new
            {
                Action = action,
                ActorUserId = actorId,
                TargetUserId = targetUserId?.ToString(),
                Email = MaskEmail(email),
                EmailHash = ShortHash(email),
                NewEmail = MaskEmail(newEmail),
                NewEmailHash = ShortHash(newEmail),
                Ip = MaskIp(context.Ip),
                UserAgentHash = ShortHash(context.UserAgent),
                // The client as it names itself, only for a request nobody signed in made: it is what
                // tells a scanner (zgrab, python-requests) from a browser; for a person the hash is enough.
                UserAgent = context.UserId == null ? Shorten(context.UserAgent, MaxUserAgentLength) : null,
                Host = context.Host,
                Path = context.Path,
                Query = Shorten(context.Query, MaxQueryLength),
                Method = context.Method,
                TraceId = context.TraceId,
                RequestId = context.RequestId,
                Reason = reason
            };

            _logger.LogInformation("Audit action: {Action}. {@Payload}", action, payload);
        }
    }

    public void TrackFailedLogin(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return;
        }

        var context = BuildContext();
        var key = BuildAttemptKey("login", email, context.Ip);
        if (TryIncrementCounter(key, _options.FailedLoginThreshold, _options.FailedLoginWindow, out var count))
        {
            LogSuspicious(
                action: "Auth.LoginFailedThreshold",
                context: context,
                details: new SecurityEventDetails(
                    Email: email,
                    Count: count,
                    Reason: "Failed login attempts exceeded threshold."));
        }
    }

    public void TrackFailedMfa(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return;
        }

        var context = BuildContext();
        var key = BuildAttemptKey("mfa", email, context.Ip);
        if (TryIncrementCounter(key, _options.FailedMfaThreshold, _options.FailedMfaWindow, out var count))
        {
            LogSuspicious(
                action: "Auth.MfaFailedThreshold",
                context: context,
                details: new SecurityEventDetails(
                    Email: email,
                    Count: count,
                    Reason: "Failed MFA attempts exceeded threshold."));
        }
    }

    public void ReportRateLimitRejection(string? policyName)
    {
        var context = BuildContext();
        LogSuspicious(
            action: "RateLimit.Rejected",
            context: context,
            details: new SecurityEventDetails(
                Reason: policyName == null
                    ? "Rate limit rejected request."
                    : $"Rate limit policy '{policyName}' rejected request.",
                PolicyName: policyName));
    }

    public void ReportGeoBlocked(string ip, string? countryCode)
    {
        var context = BuildContext(ip);
        LogSuspicious(
            action: "Geo.Blocked",
            context: context,
            details: new SecurityEventDetails(
                Reason: "Request blocked by geo policy.",
                CountryCode: countryCode));
    }

    public void TrackIpChange(Guid userId, string ip)
    {
        if (string.IsNullOrWhiteSpace(ip))
        {
            return;
        }

        var key = $"ip-change:{userId}";
        string? previousIp = null;

        lock (_cacheLock)
        {
            if (_cache.TryGetValue(key, out string? cached))
            {
                previousIp = cached;
            }

            _cache.Set(
                key,
                ip,
                new MemoryCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = _options.IpChangeWindow
                });
        }

        if (!string.IsNullOrWhiteSpace(previousIp)
            && !string.Equals(previousIp, ip, StringComparison.OrdinalIgnoreCase))
        {
            var context = BuildContext(ip);
            LogSuspicious(
                action: "Auth.IpChanged",
                context: context,
                details: new SecurityEventDetails(
                    Reason: "Request from a new IP address.",
                    OldIp: previousIp,
                    NewIp: ip));
        }
    }

    private RequestContext BuildContext(string? ipOverride = null)
    {
        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext == null)
        {
            return new RequestContext(null, ipOverride, null, null, null, null, null, null, null, null);
        }

        var userId = httpContext.User.GetUserKeyValue();

        var ip = ipOverride ?? httpContext.Connection.RemoteIpAddress?.ToString();
        var path = SecretPaths.Mask(httpContext.Request?.Path.Value);
        var method = httpContext.Request?.Method;
        var userAgent = httpContext.Request?.Headers["User-Agent"].ToString();
        var traceId = Activity.Current?.TraceId.ToString() ?? httpContext.TraceIdentifier;
        var requestId = httpContext.TraceIdentifier;
        var host = httpContext.Request?.Host.Value;
        var query = httpContext.Request?.QueryString.Value;
        // Behind Cloudflare the country is already on the request; no lookup is made for a log line.
        var country = httpContext.Request?.Headers["CF-IPCountry"].ToString();

        return new RequestContext(userId, ip, path, method, userAgent, traceId, requestId, host, query, country);
    }

    private void LogSuspicious(
        string action,
        RequestContext context,
        SecurityEventDetails details)
    {
        using (LogContext.PushProperty("EventType", "Security.Suspicious"))
        using (LogContext.PushProperty("Action", action))
        {
            var payload = new
            {
                Action = action,
                ActorUserId = context.UserId,
                Email = MaskEmail(details.Email),
                EmailHash = ShortHash(details.Email),
                Ip = MaskIp(context.Ip),
                OldIp = MaskIp(details.OldIp),
                NewIp = MaskIp(details.NewIp),
                UserAgentHash = ShortHash(context.UserAgent),
                Path = context.Path,
                Method = context.Method,
                TraceId = context.TraceId,
                RequestId = context.RequestId,
                PolicyName = details.PolicyName,
                CountryCode = details.CountryCode ?? UsableCountry(context.Country),
                Count = details.Count,
                Reason = details.Reason
            };

            _logger.LogWarning("Security event: {Action}. {@Payload}", action, payload);
        }
    }

    private bool TryIncrementCounter(string key, int threshold, TimeSpan window, out int count)
    {
        lock (_cacheLock)
        {
            if (!_cache.TryGetValue(key, out CounterState? state))
            {
                state = new CounterState();
            }
            state ??= new CounterState();

            state.Count += 1;
            count = state.Count;

            var shouldNotify = state.Count >= threshold && !state.Notified;
            if (shouldNotify)
            {
                state.Notified = true;
            }

            _cache.Set(
                key,
                state,
                new MemoryCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = window
                });

            return shouldNotify;
        }
    }

    private static string BuildAttemptKey(string type, string email, string? ip)
    {
        var normalized = NormalizeEmail(email);
        var ipPart = string.IsNullOrWhiteSpace(ip) ? "unknown" : ip;
        return $"security:{type}:{normalized}:{ipPart}";
    }

    private static string NormalizeEmail(string email)
    {
        return email.Trim().ToLowerInvariant();
    }

    private static string? MaskEmail(string? email)
    {
        if (string.IsNullOrWhiteSpace(email) || !email.Contains("@", StringComparison.Ordinal))
        {
            return null;
        }

        var parts = email.Split('@', 2, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 2)
        {
            return null;
        }

        var local = parts[0];
        var domain = parts[1];
        if (local.Length <= 2)
        {
            return $"{new string('*', local.Length)}@{domain}";
        }

        var maskedLocal = $"{local[0]}{new string('*', local.Length - 2)}{local[^1]}";
        return $"{maskedLocal}@{domain}";
    }

    private static string? MaskIp(string? ip)
    {
        if (string.IsNullOrWhiteSpace(ip))
        {
            return null;
        }

        if (ip.Contains(".", StringComparison.Ordinal))
        {
            var segments = ip.Split('.');
            if (segments.Length == 4)
            {
                return $"{segments[0]}.{segments[1]}.x.x";
            }
            return "x.x.x.x";
        }

        if (ip.Contains(":", StringComparison.Ordinal))
        {
            var segments = ip.Split(':', StringSplitOptions.RemoveEmptyEntries);
            if (segments.Length >= 3)
            {
                return $"{segments[0]}:{segments[1]}:{segments[2]}:x:x:x:x";
            }
            return "x:x:x:x";
        }

        return "x.x.x.x";
    }

    private const int MaxUserAgentLength = 160;
    private const int MaxQueryLength = 200;

    private static string? Shorten(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return value.Length <= maxLength ? value : string.Concat(value.AsSpan(0, maxLength), "…");
    }

    /// <summary>Cloudflare's two-letter code; «XX» (unknown) and «T1» (Tor) say nothing of a country.</summary>
    private static string? UsableCountry(string? code) =>
        string.IsNullOrWhiteSpace(code) || code.Length != 2 || code is "XX" or "T1" ? null : code.ToUpperInvariant();

    private static string? ShortHash(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var bytes = Encoding.UTF8.GetBytes(value);
        var hash = SHA256.HashData(bytes);
        var builder = new StringBuilder(12);
        for (var i = 0; i < 6; i++)
        {
            builder.Append(hash[i].ToString("x2"));
        }
        return builder.ToString();
    }

    private sealed class CounterState
    {
        public int Count { get; set; }
        public bool Notified { get; set; }
    }

    private sealed record SecurityEventDetails(
        string? Email = null,
        int? Count = null,
        string? Reason = null,
        string? PolicyName = null,
        string? CountryCode = null,
        string? OldIp = null,
        string? NewIp = null);

    private sealed record RequestContext(
        string? UserId,
        string? Ip,
        string? Path,
        string? Method,
        string? UserAgent,
        string? TraceId,
        string? RequestId,
        string? Host,
        string? Query,
        string? Country);
}
