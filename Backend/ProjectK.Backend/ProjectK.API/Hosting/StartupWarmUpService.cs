using AutoMapper;
using MediatR;
using ProjectK.BusinessLogic.Modules.ScoreModule.Services;
using ProjectK.Common.Interfaces.Modules.KurinModule;
using ProjectK.Common.Interfaces.Modules.MemberModule;

namespace ProjectK.API.Hosting;

/// <summary>
/// Pays the first-request cost once the server is listening, instead of on the first person who
/// opens the dashboard. <c>/health</c> touches neither MediatR, AutoMapper nor a single EF query, so
/// an instance could serve probes for a day and still be cold: on one B1 core the first dashboard
/// after a deploy took 13 s, almost all of it JIT and EF query compilation between fast SQL calls.
/// The reads use an empty key: EF caches a compiled query by its shape, not its parameter values.
/// </summary>
public sealed class StartupWarmUpService : BackgroundService
{
    private readonly IServiceProvider _services;
    private readonly IHostApplicationLifetime _lifetime;
    private readonly ILogger<StartupWarmUpService> _logger;

    public StartupWarmUpService(IServiceProvider services, IHostApplicationLifetime lifetime, ILogger<StartupWarmUpService> logger)
    {
        _services = services;
        _lifetime = lifetime;
        _logger = logger;
    }

    /// <summary>On wherever an instance serves people; off locally and under tests, where it only adds noise.</summary>
    public static bool IsEnabled(IConfiguration configuration, IHostEnvironment environment) =>
        bool.TryParse(configuration["Startup:WarmUp"], out var configured)
            ? configured
            : !environment.IsDevelopment() && !environment.IsEnvironment("Testing") && !environment.IsEnvironment("E2E");

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var started = new TaskCompletionSource();
        using (_lifetime.ApplicationStarted.Register(() => started.TrySetResult()))
        using (stoppingToken.Register(() => started.TrySetCanceled(stoppingToken)))
        {
            try
            {
                await started.Task;
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }

        var watch = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            using var scope = _services.CreateScope();
            var provider = scope.ServiceProvider;

            provider.GetRequiredService<IMapper>().ConfigurationProvider.CompileMappings();
            provider.GetRequiredService<IMediator>();

            // The dashboard path: who the person is, where they belong, and the score book that
            // two of its tiles open.
            var members = provider.GetRequiredService<IMemberDirectory>();
            await members.FindByAccountAsync(Guid.Empty, stoppingToken);
            await members.GetLevelsAsync(Guid.Empty, stoppingToken);
            await members.GetByKurinAsync(Guid.Empty, stoppingToken);
            await provider.GetRequiredService<IMembershipDirectory>().GetCurrentForAccountAsync(Guid.Empty, stoppingToken);
            await provider.GetRequiredService<ScoreBookReader>().OpenAsync(Guid.Empty, stoppingToken);

            _logger.LogInformation("Warm-up finished in {ElapsedMs} ms.", watch.ElapsedMilliseconds);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            // A failed warm-up costs only the speed of the first request; it must not take the host down.
            _logger.LogWarning(ex, "Warm-up failed after {ElapsedMs} ms.", watch.ElapsedMilliseconds);
        }
    }
}
