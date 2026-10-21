using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Azure;
using Azure.Core;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.Extensions.Logging;
using ProjectK.Common.Interfaces.Modules.InfrastructureModule;
using ProjectK.Common.Models.Records;
using ProjectK.Common.Models.Settings;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.Processing;

namespace ProjectK.Infrastructure.Services.BlobStorageService;


public interface IPhotoReferenceProvider
{
    Task<IReadOnlyCollection<string>> GetAllReferencedBlobNamesAsync(CancellationToken cancellationToken);
}

public class AzureBlobPhotoService : IPhotoService
{
    /// <summary>
    /// Decodes only what <c>ImageUploadRules</c> accepts. The format is told by the bytes, not the
    /// declared content type, so with the default configuration a TIFF sent as image/png reached the
    /// TIFF decoder; metadata (EXIF, ICC) is never used here, and its parsers are where the open
    /// ImageSharp 3.x advisories sit. 4.x fixes them but will not build without a Six Labors licence.
    /// </summary>
    private static readonly DecoderOptions PhotoDecoding = new()
    {
        Configuration = new Configuration(new PngConfigurationModule(), new JpegConfigurationModule(), new WebpConfigurationModule()),
        SkipMetadata = true
    };

    private readonly BlobContainerClient _container;
    private readonly BlobStorageOptions _options;
    private volatile bool _containerInitialized;
    private readonly SemaphoreSlim _containerInitLock = new(1, 1);
    private readonly IPhotoReferenceProvider? _referenceProvider;
    private readonly IBlobReadLinks _links;
    private readonly ILogger<AzureBlobPhotoService>? _logger;

    private readonly ConcurrentDictionary<string, bool> _usageCache = new();

    public AzureBlobPhotoService(
        BlobStorageOptions options,
        IPhotoReferenceProvider? referenceProvider,
        IBlobReadLinks links,
        ILogger<AzureBlobPhotoService>? logger = null)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        if (string.IsNullOrWhiteSpace(_options.ConnectionString))
        {
            throw new ArgumentException("Blob storage connection string is not configured.");
        }
        var blobServiceClient = new BlobServiceClient(_options.ConnectionString);
        _container = blobServiceClient.GetBlobContainerClient(_options.ContainerName);
        _referenceProvider = referenceProvider;
        _links = links;
        _logger = logger;
    }

    public async Task<PhotoUploadResult> UploadPhotoAsync(Stream photoStream, string fileName, CancellationToken cancellationToken)
        => await UploadPhotoAsync(photoStream, fileName, BlobUploadContext.MemberPhoto, cancellationToken).ConfigureAwait(false);

    public async Task<PhotoUploadResult> UploadPhotoAsync(
        Stream photoStream,
        string fileName,
        BlobUploadContext uploadContext,
        CancellationToken cancellationToken)
    {
        if (photoStream is null)
            throw new ArgumentException("Порожній вміст файлу.", nameof(photoStream));
        if (string.IsNullOrWhiteSpace(fileName))
            throw new ArgumentException("Порожня назва файлу.", nameof(fileName));
        if (uploadContext is null)
            throw new ArgumentNullException(nameof(uploadContext));

        await EnsureContainerAsync(cancellationToken).ConfigureAwait(false);

        var upload = await PrepareUploadAsync(photoStream, fileName, uploadContext, cancellationToken).ConfigureAwait(false);

        var blobName = BuildBlobName(uploadContext, upload.FinalExtension);
        var blobClient = _container.GetBlobClient(blobName);

        var headers = new BlobHttpHeaders
        {
            ContentType = upload.ContentType,
            CacheControl = "public, max-age=31536000"
        };

        await using var ms = new MemoryStream(upload.ProcessedBytes);
        await blobClient.UploadAsync(ms, new BlobUploadOptions { HttpHeaders = headers }, cancellationToken)
            .ConfigureAwait(false);

        var url = uploadContext == BlobUploadContext.FeedbackScreenshot
            ? _links.ForSharing(blobName)!
            : _links.For(blobName)!;
        return new PhotoUploadResult(blobName, url);
    }

    // byte[] overloads buffer into a MemoryStream and reuse the stream-based path,
    // so existing byte[] callers keep working while the hot path avoids the extra copy.
    public async Task<PhotoUploadResult> UploadPhotoAsync(byte[] photoBytes, string fileName, CancellationToken cancellationToken)
        => await UploadPhotoAsync(photoBytes, fileName, BlobUploadContext.MemberPhoto, cancellationToken).ConfigureAwait(false);

    public async Task<PhotoUploadResult> UploadPhotoAsync(
        byte[] photoBytes,
        string fileName,
        BlobUploadContext uploadContext,
        CancellationToken cancellationToken)
    {
        if (photoBytes is null || photoBytes.Length == 0)
            throw new ArgumentException("Порожній вміст файлу.", nameof(photoBytes));

        await using var ms = new MemoryStream(photoBytes, writable: false);
        return await UploadPhotoAsync(ms, fileName, uploadContext, cancellationToken).ConfigureAwait(false);
    }

    internal async Task<PreparedBlobUpload> PrepareUploadAsync(
        Stream photoStream,
        string fileName,
        BlobUploadContext uploadContext,
        CancellationToken cancellationToken)
    {
        return uploadContext.ProcessingMode switch
        {
            BlobUploadProcessingMode.CompressToJpeg => await PrepareJpegUploadAsync(photoStream, fileName, uploadContext, cancellationToken).ConfigureAwait(false),
            BlobUploadProcessingMode.EncodeAsPng => await PreparePngUploadAsync(photoStream, fileName, uploadContext, cancellationToken).ConfigureAwait(false),
            _ => throw new ArgumentOutOfRangeException(nameof(uploadContext), uploadContext.ProcessingMode, "Unsupported blob upload processing mode.")
        };
    }

    internal async Task<PreparedBlobUpload> PrepareUploadAsync(
        byte[] photoBytes,
        string fileName,
        BlobUploadContext uploadContext,
        CancellationToken cancellationToken)
    {
        await using var ms = new MemoryStream(photoBytes, writable: false);
        return await PrepareUploadAsync(ms, fileName, uploadContext, cancellationToken).ConfigureAwait(false);
    }

    internal async Task<(byte[] ProcessedBytes, string FinalExtension)> CompressImageAsync(byte[] photoBytes, string fileName, CancellationToken cancellationToken)
    {
        await using var ms = new MemoryStream(photoBytes, writable: false);
        var upload = await PrepareJpegUploadAsync(ms, fileName, BlobUploadContext.MemberPhoto, cancellationToken)
            .ConfigureAwait(false);

        return (upload.ProcessedBytes, upload.FinalExtension);
    }

    private async Task<PreparedBlobUpload> PrepareJpegUploadAsync(
        Stream photoStream,
        string fileName,
        BlobUploadContext uploadContext,
        CancellationToken cancellationToken)
    {
        try
        {
            using var image = await Image.LoadAsync(PhotoDecoding, photoStream, cancellationToken).ConfigureAwait(false);

            // Resize if too large (max 1920x1920)
            const int MaxDimension = 1920;
            if (image.Width > MaxDimension || image.Height > MaxDimension)
            {
                image.Mutate(x => x.Resize(new ResizeOptions
                {
                    Mode = ResizeMode.Max,
                    Size = new Size(MaxDimension, MaxDimension)
                }));
            }

            // Compress and convert to JPEG to ensure small size (Quality 75 is a good balance)
            using var msCompressed = new MemoryStream();
            var encoder = new JpegEncoder { Quality = 75 };
            await image.SaveAsync(msCompressed, encoder, cancellationToken).ConfigureAwait(false);

            byte[] processedBytes = msCompressed.ToArray();

            _logger?.LogInformation("Image {FileName} compressed to {CompressedSize} bytes", fileName, processedBytes.Length);

            return new PreparedBlobUpload(processedBytes, ".jpg", uploadContext.ContentType); // Force extension to jpg since we encoded as jpeg
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Nothing but a decoded image is ever written. A file that failed to decode used to be
            // stored under its own extension and content type — while the container was public, an
            // .html or .svg upload became a page served from the storage domain.
            _logger?.LogWarning(ex, "Uploaded file {FileName} is not a decodable image.", fileName);
            throw new InvalidOperationException("Uploaded file is not a valid image.", ex);
        }
    }

    private async Task<PreparedBlobUpload> PreparePngUploadAsync(
        Stream photoStream,
        string fileName,
        BlobUploadContext uploadContext,
        CancellationToken cancellationToken)
    {
        try
        {
            using var image = await Image.LoadAsync(PhotoDecoding, photoStream, cancellationToken).ConfigureAwait(false);
            using var msPng = new MemoryStream();
            await image.SaveAsync(msPng, new PngEncoder(), cancellationToken).ConfigureAwait(false);

            var processedBytes = msPng.ToArray();
            _logger?.LogInformation(
                "Image {FileName} encoded as PNG: {ProcessedSize} bytes",
                fileName,
                processedBytes.Length);

            return new PreparedBlobUpload(processedBytes, ".png", uploadContext.ContentType);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to encode image {FileName} as PNG.", fileName);
            throw new InvalidOperationException("Uploaded file is not a valid image.", ex);
        }
    }

    public async Task<bool> DeletePhotoAsync(string photoUrl, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(photoUrl))
            return false;

        string blobName;
        if (photoUrl.Contains("://"))
        {
            if (!TryExtractBlobName(photoUrl, out blobName))
                return false;
        }
        else
        {
            blobName = photoUrl;
        }

        await EnsureContainerAsync(cancellationToken).ConfigureAwait(false);
        var blobClient = _container.GetBlobClient(blobName);
        try
        {
            var resp = await blobClient.DeleteIfExistsAsync(DeleteSnapshotsOption.IncludeSnapshots, cancellationToken: cancellationToken)
                .ConfigureAwait(false);
            return resp.Value;
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            return false;
        }
    }

    // Helper that could be invoked externally (not part of interface) to mark a blob as in-use again.
    public async Task MarkInUseAsync(string photoUrl, bool inUse, CancellationToken cancellationToken)
    {
        if (!TryExtractBlobName(photoUrl, out var blobName))
            return;

        await EnsureContainerAsync(cancellationToken).ConfigureAwait(false);

        var blobClient = _container.GetBlobClient(blobName);
        try
        {
            var props = await blobClient.GetPropertiesAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
            var metadata = props.Value.Metadata ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            metadata[_options.UsageMetadataKey] = inUse ? "true" : "false";
            await blobClient.SetMetadataAsync(metadata, cancellationToken: cancellationToken).ConfigureAwait(false);
            _usageCache[blobName] = inUse;
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            // Ignore missing blob.
        }
    }

    public Task PrepareStorageAsync(CancellationToken cancellationToken) => EnsureContainerAsync(cancellationToken);

    private async Task EnsureContainerAsync(CancellationToken cancellationToken)
    {
        if (_containerInitialized)
            return;

        await _containerInitLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_containerInitialized)
                return;

            // Private on purpose: photos of children are read only through signed links.
            if (_options.AutoCreateContainer)
            {
                await _container.CreateIfNotExistsAsync(PublicAccessType.None, cancellationToken: cancellationToken)
                    .ConfigureAwait(false);
            }

            await ClosePublicAccessAsync(cancellationToken).ConfigureAwait(false);

            _containerInitialized = true;
        }
        finally
        {
            _containerInitLock.Release();
        }
    }

    // Containers made before links were signed allowed anonymous reads. Setting the policy also drops
    // stored access policies; none are used, every link is signed with the account key.
    private async Task ClosePublicAccessAsync(CancellationToken cancellationToken)
    {
        BlobContainerProperties properties;
        try
        {
            properties = (await _container.GetPropertiesAsync(cancellationToken: cancellationToken).ConfigureAwait(false)).Value;
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            return;
        }

        if (properties.PublicAccess is null or PublicAccessType.None)
            return;

        await _container.SetAccessPolicyAsync(PublicAccessType.None, cancellationToken: cancellationToken).ConfigureAwait(false);
        _logger?.LogWarning("Closed public access on blob container {Container}; photos are now read through signed links.", _container.Name);
    }

    internal string BuildBlobName(BlobUploadContext uploadContext, string extension)
        => BuildBlobName(uploadContext, extension, DateTime.UtcNow);

    internal string BuildBlobName(BlobUploadContext uploadContext, string extension, DateTime utcNow)
    {
        var folder = NormalizeFolder(uploadContext.Folder);
        var datePath = $"{utcNow:yyyy'/'MM'/'dd}";
        var prefix = $"{folder}/{datePath}";
        return $"{prefix}/{Guid.NewGuid():N}{extension}";
    }

    // Select blobName from full URL (container/name...). Azure/Azurite
    private static bool TryExtractBlobName(string photoUrl, out string blobName)
    {
        blobName = string.Empty;
        if (!Uri.TryCreate(photoUrl, UriKind.Absolute, out var uri))
            return false;

        // /<container>/<blobName>
        var parts = uri.AbsolutePath.Trim('/').Split('/', 2, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2) return false;

        blobName = parts[1];
        return true;
    }

    private static string NormalizeFolder(string folder)
    {
        if (string.IsNullOrWhiteSpace(folder))
            throw new ArgumentException("Blob upload folder is required.", nameof(folder));

        var normalized = folder.Trim().Trim('/');
        if (normalized.Length == 0 || normalized.Contains('\\') || normalized.Contains("..", StringComparison.Ordinal))
            throw new ArgumentException("Blob upload folder is invalid.", nameof(folder));

        return normalized;
    }

    private IEnumerable<string> GetCleanupPrefixes()
        => BlobUploadFolders.ScenarioFolders;
}

internal sealed record PreparedBlobUpload(byte[] ProcessedBytes, string FinalExtension, string ContentType);
