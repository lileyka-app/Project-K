using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ProjectK.Infrastructure.Seeding;

namespace ProjectK.Infrastructure.BackgroundServices;

/// <summary>
/// Puts the public demo back to its seeded state once a night. Only the Demo environment does
/// anything here; everywhere else the service starts, sees the tier, and goes quiet. The reset is
/// the seeder itself: it wipes the demo kurin and plants it again, so whatever visitors typed
/// during the day is gone by morning.
/// </summary>
public sealed class DemoResetBackgroundService : BackgroundService
{
    public const string Environment = "Demo";

    private readonly IServiceProvider _services;
    private readonly IHostEnvironment _environment;
    private readonly IHostApplicationLifetime _lifetime;
    private readonly ILogger<DemoResetBackgroundService> _logger;
    private readonly int _resetHourUtc;

    public DemoResetBackgroundService(
        IServiceProvider services,
        IHostEnvironment environment,
        IHostApplicationLifetime lifetime,
        IConfiguration configuration,
        ILogger<DemoResetBackgroundService> logger)
    {
        _services = services;
        _environment = environment;
        _lifetime = lifetime;
        _logger = logger;
        _resetHourUtc = Math.Clamp(configuration.GetValue("Demo:ResetHourUtc", 3), 0, 23);
    }

    /// <summary>The next moment the clock reads the reset hour, always in the future.</summary>
    public static DateTime NextResetAfter(DateTime nowUtc, int resetHourUtc)
    {
        var today = new DateTime(nowUtc.Year, nowUtc.Month, nowUtc.Day, resetHourUtc, 0, 0, DateTimeKind.Utc);
        return today > nowUtc ? today : today.AddDays(1);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!string.Equals(_environment.EnvironmentName, Environment, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        _logger.LogInformation("Demo reset scheduled daily at {Hour:00}:00 UTC.", _resetHourUtc);

        // The startup seed runs before any hosted service starts, so the probe and badge catalogue
        // is still empty then and the demo kurin gets no проби or вмілості. Once the host has
        // started, the catalogue is loaded: plant the kurin again so a fresh stand is complete
        // from its first minute, not from the first night.
        if (await WaitForStartAsync(stoppingToken))
        {
            await ResetAsync("after start");
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            var wait = NextResetAfter(DateTime.UtcNow, _resetHourUtc) - DateTime.UtcNow;
            try
            {
                await Task.Delay(wait, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            await ResetAsync("nightly");
        }
    }

    private async Task<bool> WaitForStartAsync(CancellationToken stoppingToken)
    {
        var started = new TaskCompletionSource();
        using var onStarted = _lifetime.ApplicationStarted.Register(() => started.TrySetResult());
        using var onStopping = stoppingToken.Register(() => started.TrySetCanceled());
        try
        {
            await started.Task;
            return true;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }

    private async Task ResetAsync(string when)
    {
        try
        {
            _logger.LogInformation("Demo reset ({When}): reseeding the demo kurin.", when);
            await DataSeeder.SeedAsync(_services);
            _logger.LogInformation("Demo reset done.");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Demo reset failed; the stand keeps the previous data until the next attempt.");
        }
    }
}
