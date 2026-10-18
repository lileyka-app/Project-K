using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ProjectK.Common.Models.Settings;

namespace ProjectK.Infrastructure.Services.Telemetry;

/// <summary>
/// Pings the status service once a day with a <see cref="TelemetryReport"/>: the first ping a few
/// minutes after startup, then every <see cref="TelemetryOptions.IntervalHours"/>. A failed ping is
/// logged and skipped; it never affects the API. The answer names the latest release, and an older
/// installation says so in its log.
/// </summary>
internal sealed class TelemetryBackgroundService : BackgroundService
{
    public const string HttpClientName = "telemetry";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly IServiceProvider _services;
    private readonly IHttpClientFactory _httpClients;
    private readonly TelemetryCounters _counters;
    private readonly TelemetryOptions _options;
    private readonly IConfiguration _configuration;
    private readonly IHostEnvironment _environment;
    private readonly ILogger<TelemetryBackgroundService> _logger;
    private readonly DateTime _startedUtc = DateTime.UtcNow;

    public TelemetryBackgroundService(
        IServiceProvider services,
        IHttpClientFactory httpClients,
        TelemetryCounters counters,
        IOptions<TelemetryOptions> options,
        IConfiguration configuration,
        IHostEnvironment environment,
        ILogger<TelemetryBackgroundService> logger)
    {
        _services = services;
        _httpClients = httpClients;
        _counters = counters;
        _options = options.Value;
        _configuration = configuration;
        _environment = environment;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Telemetry: an anonymous daily ping goes to {Endpoint}; Telemetry:Enabled=false turns it off.", _options.Endpoint);
        try
        {
            await Task.Delay(TimeSpan.FromMinutes(Math.Max(0, _options.InitialDelayMinutes)), stoppingToken);
            while (!stoppingToken.IsCancellationRequested)
            {
                await PingAsync(stoppingToken);
                await Task.Delay(TimeSpan.FromHours(Math.Max(1, _options.IntervalHours)), stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
    }

    internal async Task PingAsync(CancellationToken cancellationToken)
    {
        try
        {
            var version = _configuration["ReleaseInfo:Version"] ?? "unknown";
            TelemetryReport report;
            using (var scope = _services.CreateScope())
            {
                var collector = scope.ServiceProvider.GetRequiredService<TelemetryCollector>();
                var id = await collector.InstanceIdAsync(cancellationToken);
                report = await collector.CollectAsync(id, version, _environment.EnvironmentName, _counters.TakeAndReset(), DateTime.UtcNow - _startedUtc, DateTime.UtcNow, cancellationToken);
            }

            using var response = await _httpClients.CreateClient(HttpClientName).PostAsJsonAsync(_options.Endpoint, report, Json, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Telemetry ping answered HTTP {Status}.", (int)response.StatusCode);
                return;
            }

            var answer = await response.Content.ReadFromJsonAsync<PingAnswer>(Json, cancellationToken);
            if (answer?.Latest is { } latest && IsOlder(report.Version, latest))
            {
                _logger.LogWarning("Lilejka {Latest} is out; this installation runs {Version}.", latest, report.Version);
            }
            else
            {
                _logger.LogInformation("Telemetry ping sent.");
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning(ex, "Telemetry ping failed; the next one goes in {Hours} h.", _options.IntervalHours);
        }
    }

    /// <summary>"v1.9.0" is older than "v1.10.0" and "1.2" means "1.2.0"; anything that is not a plain vX.Y or vX.Y.Z is never older.</summary>
    internal static bool IsOlder(string version, string latest)
    {
        static Version? Parse(string v) =>
            System.Version.TryParse(v.TrimStart('v', 'V'), out var parsed) && parsed.Revision < 0
                ? new Version(parsed.Major, parsed.Minor, Math.Max(parsed.Build, 0))
                : null;

        return Parse(version) is { } current && Parse(latest) is { } newest && current < newest;
    }

    private sealed record PingAnswer(bool Ok, string? Latest);
}
