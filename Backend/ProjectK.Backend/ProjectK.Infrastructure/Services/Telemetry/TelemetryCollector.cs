using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using ProjectK.Common.Entities.InfrastructureModule;
using ProjectK.Common.Models;
using ProjectK.Infrastructure.DbContexts;

namespace ProjectK.Infrastructure.Services.Telemetry;

/// <summary>Reads the counts a ping carries. Nothing it returns names a person or a kurin.</summary>
internal sealed partial class TelemetryCollector
{
    private readonly AppDbContext _db;

    public TelemetryCollector(AppDbContext db)
    {
        _db = db;
    }

    /// <summary>The installation's random id: made once and kept in <c>SystemSettings</c>.</summary>
    public async Task<string> InstanceIdAsync(CancellationToken cancellationToken)
    {
        var setting = await _db.SystemSettings.FirstOrDefaultAsync(s => s.Key == SystemSettingKeys.TelemetryInstanceId, cancellationToken);
        if (setting is not null && Guid.TryParse(setting.Value, out var existing))
        {
            return existing.ToString();
        }

        var id = Guid.NewGuid().ToString();
        if (setting is null)
        {
            _db.SystemSettings.Add(new SystemSetting { Key = SystemSettingKeys.TelemetryInstanceId, Value = id, UpdatedAtUtc = DateTime.UtcNow });
        }
        else
        {
            setting.Value = id;
            setting.UpdatedAtUtc = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync(cancellationToken);
        return id;
    }

    public async Task<TelemetryReport> CollectAsync(
        string instanceId,
        string version,
        string environmentName,
        (long Requests, long Errors) traffic,
        TimeSpan uptime,
        DateTime nowUtc,
        CancellationToken cancellationToken)
    {
        var krais = await _db.Kurins.Select(k => k.KraiCode).Distinct().ToListAsync(cancellationToken);
        var kurins = await _db.Kurins.CountAsync(cancellationToken);
        var members = await _db.Members.CountAsync(cancellationToken);

        // A sign-in or a refresh mints a session row; a refresh rotates it on every call and ended
        // rows stay for 30 days (AuditCleanupBackgroundService), so the rows cover a month.
        var monthAgo = nowUtc.AddDays(-30);
        var weekAgo = nowUtc.AddDays(-7);
        var sessions = _db.UserRefreshTokens.Where(t => t.CreatedDate >= monthAgo);
        var active30 = await sessions.Select(t => t.UserId).Distinct().CountAsync(cancellationToken);
        var active7 = await sessions.Where(t => t.CreatedDate >= weekAgo).Select(t => t.UserId).Distinct().CountAsync(cancellationToken);

        return new TelemetryReport(
            instanceId,
            VersionPattern().IsMatch(version) ? version : "unknown",
            Kind(environmentName),
            krais.Select(k => k.Trim().ToLowerInvariant()).Where(k => KraiPattern().IsMatch(k)).Distinct().Order().Take(20).ToList(),
            kurins,
            members,
            active7,
            active30,
            traffic.Requests,
            traffic.Errors,
            (int)Math.Max(0, uptime.TotalHours));
    }

    /// <summary>"prod" and "selfhost" for the two that matter; anything else as the lowercase name.</summary>
    internal static string Kind(string environmentName)
    {
        var name = environmentName.Trim().ToLowerInvariant();
        name = name switch
        {
            "production" => "prod",
            _ => NonKindChars().Replace(name, "-"),
        };
        name = name.Trim('-');
        if (name.Length > 20)
        {
            name = name[..20];
        }

        return name.Length > 0 && char.IsAsciiLetterLower(name[0]) ? name : "other";
    }

    [GeneratedRegex("^[0-9A-Za-z][0-9A-Za-z.+_-]{0,39}$")]
    private static partial Regex VersionPattern();

    [GeneratedRegex("^[a-z]{2,8}$")]
    private static partial Regex KraiPattern();

    [GeneratedRegex("[^a-z0-9-]+")]
    private static partial Regex NonKindChars();
}
