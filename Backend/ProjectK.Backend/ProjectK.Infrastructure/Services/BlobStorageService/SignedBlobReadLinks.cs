using Azure.Storage.Blobs;
using Azure.Storage.Sas;
using ProjectK.Common.Contracts.InfrastructureModule;
using ProjectK.Common.Models.Records;
using ProjectK.Common.Models.Settings;

namespace ProjectK.Infrastructure.Services.BlobStorageService;

/// <summary>
/// Signs read-only links to single blobs with the account key from the connection string.
/// <para>
/// A link's start and expiry come from a fixed window, not from the moment it is asked for: the
/// signature is a pure function of them, so every response in a window carries the same link and
/// the browser serves the image from its cache instead of downloading it again.
/// </para>
/// </summary>
public sealed class SignedBlobReadLinks : IBlobReadLinks
{
    // Lets a link work on a host whose clock runs a little behind the storage service.
    private static readonly TimeSpan ClockSkew = TimeSpan.FromMinutes(5);

    private readonly BlobContainerClient _container;
    private readonly BlobStorageOptions _options;
    private readonly TimeProvider _time;

    public SignedBlobReadLinks(BlobStorageOptions options, TimeProvider time)
    {
        _options = options;
        _time = time;
        _container = new BlobServiceClient(options.ConnectionString).GetBlobContainerClient(options.ContainerName);

        if (!_container.CanGenerateSasUri)
        {
            throw new InvalidOperationException(
                "Blob storage connection string has no AccountKey, so photo links cannot be signed. " +
                "Use a connection string with the account key.");
        }

        if (options.ReadLinkLifetime <= TimeSpan.Zero || options.SharedLinkLifetime <= TimeSpan.Zero)
        {
            throw new InvalidOperationException("Blob link lifetimes must be positive.");
        }
    }

    public string? For(string? blobName) => Sign(blobName, _options.ReadLinkLifetime);

    public string? ForSharing(string? blobName) => Sign(blobName, _options.SharedLinkLifetime);

    private string? Sign(string? blobName, TimeSpan lifetime)
    {
        if (string.IsNullOrWhiteSpace(blobName))
        {
            return null;
        }

        var window = TimeSpan.FromTicks(lifetime.Ticks / 2);
        var now = _time.GetUtcNow();
        var windowStart = new DateTimeOffset(now.UtcTicks - now.UtcTicks % window.Ticks, TimeSpan.Zero);

        var blob = _container.GetBlobClient(blobName);
        var sas = new BlobSasBuilder(BlobSasPermissions.Read, windowStart + lifetime)
        {
            BlobContainerName = _container.Name,
            BlobName = blobName,
            Resource = "b",
            StartsOn = windowStart - ClockSkew,
            Protocol = blob.Uri.Scheme == Uri.UriSchemeHttps ? SasProtocol.Https : SasProtocol.HttpsAndHttp
        };

        var signed = blob.GenerateSasUri(sas);
        var address = BlobPublicUrl.Build(_options.PublicBaseUrl, blobName, blob.Uri.GetLeftPart(UriPartial.Path))!;
        return $"{address}{signed.Query}";
    }
}
