using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ProjectK.Common.Models.Records;
using ProjectK.Common.Models.Settings;

namespace ProjectK.Infrastructure.Services.BlobStorageService.OrphanCleanup;

// Background service for cleaning up orphaned blob files (photos).
// Periodically compares the list of blobs with the set of references in the database.
public sealed class OrphanPhotoCleanupService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<OrphanPhotoCleanupService> _logger;
    private readonly BlobStorageOptions _blobOptions;
    private readonly OrphanCleanupOptions _options;
    private readonly SemaphoreSlim _runLock = new(1, 1);
    private readonly Random _rnd = new();

    public OrphanPhotoCleanupService(
        IServiceScopeFactory scopeFactory,
        ILogger<OrphanPhotoCleanupService> logger,
        BlobStorageOptions blobOptions,
        IOptions<OrphanCleanupOptions> cleanupOptions)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _blobOptions = blobOptions;
        _options = cleanupOptions.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled)
        {
            _logger.LogInformation("OrphanPhotoCleanupService disabled (Enabled=false).");
            return;
        }

        _logger.LogInformation("OrphanPhotoCleanupService started. Interval: {Interval}, Grace: {Grace}, MaxDeletes: {Max}",
            _options.Interval, _options.GracePeriod, _options.MaxDeletesPerRun);

        // Initial small jitter to avoid mass start after deployment
        // (could be removed if not needed)
        await Task.Delay(TimeSpan.FromSeconds(_rnd.Next(0, _options.JitterSeconds + 1)), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunOnceAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                // ignore - shutting down
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while executing orphan photo cleanup.");
            }

            var jitter = TimeSpan.FromSeconds(_rnd.Next(0, _options.JitterSeconds + 1));
            var delay = _options.Interval + jitter;

            try
            {
                await Task.Delay(delay, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        _logger.LogInformation("OrphanPhotoCleanupService stopped.");
    }

    private async Task RunOnceAsync(CancellationToken ct)
    {
        if (!await _runLock.WaitAsync(0, ct))
        {
            _logger.LogWarning("Previous cleanup cycle still running – skipping this run.");
            return;
        }

        try
        {
            var start = DateTime.UtcNow;
            _logger.LogInformation("Starting orphan photo check...");

            using var scope = _scopeFactory.CreateScope();
            var referenceProvider = scope.ServiceProvider.GetRequiredService<IPhotoReferenceProvider>();

            var referencedBlobNames = await GetReferencedBlobNamesAsync(referenceProvider, ct);
            var container = await GetBlobContainerAsync(ct);

            if (container != null)
            {
                var allBlobs = await GetAllBlobsAsync(container, ct);
                var orphans = GetOrphanBlobs(allBlobs, referencedBlobNames);

                await ProcessOrphanBlobsAsync(container, orphans, ct);
            }

            var elapsed = DateTime.UtcNow - start;
            _logger.LogInformation("Cleanup finished. Duration {Elapsed} sec.", elapsed.TotalSeconds);
        }
        finally
        {
            _runLock.Release();
        }
    }

    private static async Task<HashSet<string>> GetReferencedBlobNamesAsync(IPhotoReferenceProvider referenceProvider, CancellationToken ct)
    {
        var referencedBlobNames = await referenceProvider.GetAllReferencedBlobNamesAsync(ct)
            .ConfigureAwait(false);

        return new HashSet<string>(referencedBlobNames, StringComparer.Ordinal);
    }

    private async Task<BlobContainerClient?> GetBlobContainerAsync(CancellationToken ct)
    {
        var blobService = new BlobServiceClient(_blobOptions.ConnectionString);
        var container = blobService.GetBlobContainerClient(_blobOptions.ContainerName);

        if (!await container.ExistsAsync(ct))
        {
            _logger.LogInformation("Container {Container} does not exist – cleanup skipped.", _blobOptions.ContainerName);
            return null;
        }

        return container;
    }

    private async Task<List<BlobItem>> GetAllBlobsAsync(BlobContainerClient container, CancellationToken ct)
    {
        var allBlobs = new List<BlobItem>();

        foreach (var prefix in GetCleanupPrefixes())
        {
            await foreach (var pageBlob in container.GetBlobsAsync(
                                traits: BlobTraits.None,
                                states: BlobStates.None,
                                prefix: prefix,
                                cancellationToken: ct))
            {
                allBlobs.Add(pageBlob);
            }
        }

        return allBlobs
            .DistinctBy(blob => blob.Name)
            .ToList();
    }

    private List<string> GetOrphanBlobs(List<BlobItem> allBlobs, HashSet<string> referencedSet)
    {
        _logger.LogInformation("Total blob count in container {Count}, referenced in DB {Referenced}.",
            allBlobs.Count, referencedSet.Count);

        var now = DateTimeOffset.UtcNow;
        var graceThreshold = now - _options.GracePeriod;

        var orphans = allBlobs
            .Where(b => !referencedSet.Contains(b.Name))
            .Where(b => IsOldEnoughForDeletion(b, graceThreshold))
            .Select(b => b.Name)
            .Take(_options.MaxDeletesPerRun)
            .ToList();

        return orphans;
    }

    private static bool IsOldEnoughForDeletion(BlobItem blob, DateTimeOffset graceThreshold)
    {
        // Check age (LastModified can be null - then we do not delete for safety)
        return blob.Properties.LastModified is { } lastModified && lastModified < graceThreshold;
    }

    private async Task ProcessOrphanBlobsAsync(BlobContainerClient container, List<string> orphans, CancellationToken ct)
    {
        if (orphans.Count == 0)
        {
            _logger.LogInformation("No orphan blobs found for deletion.");
            return;
        }

        _logger.LogInformation("Found {Count} orphan blobs for deletion (per-run limit {Limit}). DryRun={DryRun}",
            orphans.Count, _options.MaxDeletesPerRun, _options.DryRun);

        if (_options.DryRun)
        {
            LogDryRunResults(orphans);
            return;
        }

        var deleted = await DeleteOrphanBlobsAsync(container, orphans, ct);

        _logger.LogInformation("Deleted {Deleted} of {Candidates} candidates.",
            deleted, orphans.Count);
    }

    private void LogDryRunResults(List<string> orphans)
    {
        foreach (var name in orphans)
            _logger.LogInformation("DryRun: orphan (not deleting) {BlobName}", name);
    }

    private async Task<int> DeleteOrphanBlobsAsync(BlobContainerClient container, List<string> orphans, CancellationToken ct)
    {
        int deleted = 0;

        foreach (var name in orphans)
        {
            try
            {
                var client = container.GetBlobClient(name);
                var resp = await client.DeleteIfExistsAsync(DeleteSnapshotsOption.IncludeSnapshots, cancellationToken: ct);

                if (resp.Value)
                    deleted++;
                else
                    _logger.LogDebug("Blob {Blob} already absent (possibly deleted in parallel).", name);
            }
            catch (RequestFailedException ex)
            {
                _logger.LogWarning(ex, "Error deleting blob {Blob}", name);
            }
        }

        return deleted;
    }

    private static IEnumerable<string> GetCleanupPrefixes()
        => BlobUploadFolders.ScenarioFolders;
}
