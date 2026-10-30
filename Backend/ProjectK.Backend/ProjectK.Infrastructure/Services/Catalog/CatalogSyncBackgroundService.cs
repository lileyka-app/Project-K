using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ProjectK.Common.Models.Settings;
using ProjectK.Infrastructure.DbContexts;

namespace ProjectK.Infrastructure.Services.Catalog;

/// <summary>
/// Keeps <see cref="CatalogStore"/> current. At startup it loads the copy on disk and, when there is
/// none, waits for the first fetch so the API does not open with an empty catalogue; when that fetch
/// fails too, it falls back to the copy built into the assembly. After that it asks the service
/// again every <see cref="CatalogOptions.RefreshMinutes"/>; an unchanged catalogue costs a 304. A
/// failed fetch keeps what is already loaded. Until the service has answered once (or a copy on disk
/// stands in for it), it retries sooner: one minute, doubling up to the normal interval.
/// </summary>
internal sealed class CatalogSyncBackgroundService : BackgroundService
{
    internal const string SeedResourceName = "ProjectK.Infrastructure.Catalog.Seed.json";
    internal static readonly TimeSpan FirstRetry = TimeSpan.FromMinutes(1);

    private readonly CatalogStore _store;
    private readonly CatalogClient _client;
    private readonly CatalogOptions _options;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<CatalogSyncBackgroundService> _logger;
    private readonly string _snapshotPath;
    private string? _etag;
    private bool _triedAtStart;
    private bool _hasServiceCopy;
    private int _failedRetries;

    public CatalogSyncBackgroundService(
        CatalogStore store,
        CatalogClient client,
        IOptions<CatalogOptions> options,
        IServiceScopeFactory scopeFactory,
        ILogger<CatalogSyncBackgroundService> logger)
    {
        _store = store;
        _client = client;
        _options = options.Value;
        _scopeFactory = scopeFactory;
        _logger = logger;
        _snapshotPath = Path.IsPathRooted(_options.SnapshotPath)
            ? _options.SnapshotPath
            : Path.Combine(AppContext.BaseDirectory, _options.SnapshotPath);
    }

    public override async Task StartAsync(CancellationToken cancellationToken)
    {
        await LoadKraiNamesAsync(cancellationToken);
        LoadSnapshot();
        if (!_store.IsLoaded)
        {
            _triedAtStart = true;
            await RefreshAsync(cancellationToken);
            if (!_store.IsLoaded)
            {
                LoadSeed();
            }
        }

        await base.StartAsync(cancellationToken);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_triedAtStart)
        {
            await RefreshAsync(stoppingToken);
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(NextDelay(), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            await RefreshAsync(stoppingToken);
        }
    }

    /// <summary>
    /// The normal interval once the store holds the service's catalogue; before that a short retry
    /// that doubles with each failure and never exceeds the normal interval.
    /// </summary>
    internal TimeSpan NextDelay()
    {
        var interval = TimeSpan.FromMinutes(Math.Max(1, _options.RefreshMinutes));
        if (_hasServiceCopy)
        {
            return interval;
        }

        var retry = FirstRetry * Math.Pow(2, Math.Clamp(_failedRetries - 1, 0, 16));
        return retry < interval ? retry : interval;
    }

    /// <summary>One fetch. True when the store holds the service's current catalogue afterwards.</summary>
    internal async Task<bool> RefreshAsync(CancellationToken cancellationToken)
    {
        // Another instance may have added or renamed a край since; this one only hears of its own.
        await LoadKraiNamesAsync(cancellationToken);
        var refreshed = await FetchAsync(cancellationToken);
        if (refreshed)
        {
            _hasServiceCopy = true;
            _failedRetries = 0;
        }
        else if (!_hasServiceCopy)
        {
            _failedRetries++;
        }

        return refreshed;
    }

    private async Task<bool> FetchAsync(CancellationToken cancellationToken)
    {
        try
        {
            var fetched = await _client.FetchAsync(_store.IsLoaded ? _etag : null, cancellationToken);
            if (fetched is null)
            {
                _logger.LogDebug("Catalogue unchanged (revision {Revision}).", _store.State.Revision);
                return true;
            }

            var (body, etag) = fetched.Value;
            var state = CatalogMapper.ToState(CatalogClient.Parse(body), _options.BaseUrl, DateTimeOffset.UtcNow);
            if (state.Badges.Count == 0 && state.Probes.Count == 0 && _store.IsLoaded)
            {
                _logger.LogWarning("The catalogue answered with no вмілості and no проби; keeping revision {Revision}.", _store.State.Revision);
                return false;
            }

            _store.Replace(state);
            _etag = etag;
            SaveSnapshot(body, etag);
            _logger.LogInformation(
                "Catalogue revision {Revision} loaded from {Url}: {Badges} вмілостей, {Probes} проб.",
                state.Revision, _client.CatalogUrl, state.Badges.Count, state.Probes.Count);
            return true;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Could not fetch the catalogue from {Url}; keeping revision {Revision} ({Badges} вмілостей).",
                _client.CatalogUrl, _store.State.Revision, _store.State.Badges.Count);
            return false;
        }
    }

    /// <summary>The names the <c>Krais</c> table holds; until it answers, the seeded ones stand.</summary>
    private async Task LoadKraiNamesAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var names = await db.Krais.AsNoTracking()
                .Select(krai => new { krai.Code, krai.Name })
                .ToListAsync(cancellationToken);
            _store.NameAll(names.Select(krai => KeyValuePair.Create(krai.Code, krai.Name)));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not read the краї names; вмілості keep the names they show.");
        }
    }

    private void LoadSnapshot()
    {
        if (!File.Exists(_snapshotPath))
        {
            return;
        }

        try
        {
            var body = File.ReadAllBytes(_snapshotPath);
            var fetchedAt = new DateTimeOffset(File.GetLastWriteTimeUtc(_snapshotPath), TimeSpan.Zero);
            _store.Replace(CatalogMapper.ToState(CatalogClient.Parse(body), _options.BaseUrl, fetchedAt));
            _etag = File.Exists(EtagPath) ? File.ReadAllText(EtagPath).Trim() : null;
            _hasServiceCopy = _store.IsLoaded;
            _logger.LogInformation("Catalogue revision {Revision} loaded from {Path}.", _store.State.Revision, _snapshotPath);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Ignoring the unreadable catalogue copy at {Path}.", _snapshotPath);
        }
    }

    // The built-in copy is never written to disk: there it would pass for the service's answer on
    // the next start and slow the retries down to the normal interval.
    private void LoadSeed()
    {
        try
        {
            using var stream = typeof(CatalogSyncBackgroundService).Assembly.GetManifestResourceStream(SeedResourceName)
                ?? throw new InvalidOperationException($"The assembly carries no {SeedResourceName}.");
            using var buffer = new MemoryStream();
            stream.CopyTo(buffer);
            _store.Replace(CatalogMapper.ToState(CatalogClient.Parse(buffer.ToArray()), _options.BaseUrl, null));
            _logger.LogWarning(
                "The catalogue service did not answer; serving the built-in copy (revision {Revision}) until it does.",
                _store.State.Revision);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Could not load the built-in catalogue copy; the catalogue stays empty until the service answers.");
        }
    }

    private void SaveSnapshot(byte[] body, string? etag)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_snapshotPath)!);
            var temp = _snapshotPath + ".tmp";
            File.WriteAllBytes(temp, body);
            File.Move(temp, _snapshotPath, overwrite: true);
            if (etag is null)
            {
                File.Delete(EtagPath);
            }
            else
            {
                File.WriteAllText(EtagPath, etag);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not save the catalogue copy to {Path}; the next restart fetches it again.", _snapshotPath);
        }
    }

    private string EtagPath => _snapshotPath + ".etag";
}
