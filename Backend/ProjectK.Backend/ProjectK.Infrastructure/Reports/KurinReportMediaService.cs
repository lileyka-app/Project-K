using Azure;
using Azure.Storage.Blobs;
using Microsoft.Extensions.Logging;
using ProjectK.Common.Contracts.KurinModule;
using ProjectK.Common.Options.InfrastructureModule;

namespace ProjectK.Infrastructure.Reports;

public sealed class KurinReportMediaService : IKurinReportMedia
{
    private const long MaxReportImageBytes = 5 * 1024 * 1024;

    private readonly BlobStorageOptions _options;
    private readonly ILogger<KurinReportMediaService> _logger;
    private readonly Lazy<BlobContainerClient> _container;

    public KurinReportMediaService(
        BlobStorageOptions options,
        ILogger<KurinReportMediaService> logger)
    {
        _options = options;
        _logger = logger;
        _container = new Lazy<BlobContainerClient>(() =>
        {
            var client = new BlobServiceClient(_options.ConnectionString);
            return client.GetBlobContainerClient(_options.ContainerName);
        });
    }

    public async Task<byte[]?> TryDownloadAsync(string? blobName, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(blobName))
        {
            return null;
        }

        try
        {
            var blob = _container.Value.GetBlobClient(blobName);
            var properties = await blob.GetPropertiesAsync(cancellationToken: cancellationToken);

            if (properties.Value.ContentLength > MaxReportImageBytes)
            {
                _logger.LogWarning(
                    "Skipping report image {BlobName}: content length {ContentLength} exceeds {MaxReportImageBytes}",
                    blobName,
                    properties.Value.ContentLength,
                    MaxReportImageBytes);
                return null;
            }

            var response = await blob.DownloadContentAsync(cancellationToken);
            return response.Value.Content.ToArray();
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            _logger.LogWarning("Skipping missing report image blob {BlobName}", blobName);
            return null;
        }
        catch (Exception ex) when (ex is RequestFailedException or InvalidOperationException)
        {
            _logger.LogWarning(ex, "Failed to load report image blob {BlobName}", blobName);
            return null;
        }
    }
}
