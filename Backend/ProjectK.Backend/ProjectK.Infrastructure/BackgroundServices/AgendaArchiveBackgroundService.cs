using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ProjectK.Common.Enums.AgendaModule;
using ProjectK.Infrastructure.DbContexts;

namespace ProjectK.Infrastructure.BackgroundServices;

/// <summary>
/// Once a day, by each kurin's own policy: done tasks that have sat on the board long enough move to
/// the archive, and archived tasks kept past the retention are deleted for good. A kurin that set
/// either period to empty keeps that step off. Events are never touched — the calendar is history.
/// </summary>
public class AgendaArchiveBackgroundService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<AgendaArchiveBackgroundService> _logger;
    private readonly TimeProvider _time;
    private readonly TimeSpan _checkInterval = TimeSpan.FromHours(24);

    public AgendaArchiveBackgroundService(IServiceProvider serviceProvider, ILogger<AgendaArchiveBackgroundService> logger, TimeProvider time)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _time = time;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await SweepAsync(_time.GetUtcNow().UtcDateTime, stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred archiving or purging agenda tasks.");
            }

            await Task.Delay(_checkInterval, stoppingToken);
        }
    }

    public async Task SweepAsync(DateTime nowUtc, CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var policies = await context.Kurins
            .Select(k => new { k.KurinKey, k.TaskAutoArchiveAfterDays, k.TaskArchivePurgeAfterDays })
            .ToListAsync(cancellationToken);

        int archived = 0, purged = 0;
        foreach (var policy in policies)
        {
            if (policy.TaskAutoArchiveAfterDays is { } archiveDays)
            {
                var doneBefore = nowUtc.AddDays(-archiveDays);
                archived += await context.AgendaItems
                    .Where(a => a.KurinKey == policy.KurinKey
                                && a.Kind == AgendaItemKind.Task
                                && a.ArchivedAtUtc == null
                                && a.Status == AgendaItemStatus.Done
                                && a.CompletedAtUtc != null
                                && a.CompletedAtUtc <= doneBefore)
                    .ExecuteUpdateAsync(s => s.SetProperty(a => a.ArchivedAtUtc, nowUtc), cancellationToken);
            }

            if (policy.TaskArchivePurgeAfterDays is { } purgeDays)
            {
                var archivedBefore = nowUtc.AddDays(-purgeDays);
                // Targets, parts and answers go with the task by cascade.
                purged += await context.AgendaItems
                    .Where(a => a.KurinKey == policy.KurinKey
                                && a.Kind == AgendaItemKind.Task
                                && a.ArchivedAtUtc != null
                                && a.ArchivedAtUtc <= archivedBefore)
                    .ExecuteDeleteAsync(cancellationToken);
            }
        }

        if (archived > 0 || purged > 0)
        {
            _logger.LogInformation("Archived {Archived} done tasks, purged {Purged} archived tasks.", archived, purged);
        }
    }
}
