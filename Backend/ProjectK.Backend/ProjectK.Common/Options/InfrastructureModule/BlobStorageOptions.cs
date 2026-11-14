namespace ProjectK.Common.Options.InfrastructureModule;

/// <summary>
/// Options for both Azure Blob Storage and the Azurite emulator.
/// </summary>
public sealed class BlobStorageOptions
{
    /// <summary>
    /// Full connection string. Azurite accepts <c>UseDevelopmentStorage=true</c>, or an explicit
    /// <c>DefaultEndpointsProtocol=...;AccountName=...;AccountKey=...;BlobEndpoint=...</c>.
    /// </summary>
    public string ConnectionString { get; init; } = string.Empty;

    public string ContainerName { get; init; } = "photos";

    /// <summary>
    /// Where the browser reaches the container when that is not the storage endpoint itself, such as
    /// Azurite behind a proxy. Signed links keep their signature: it covers the blob path, not the host.
    /// </summary>
    public string? PublicBaseUrl { get; init; }

    public bool AutoCreateContainer { get; init; } = true;

    /// <summary>
    /// How long a link from <c>IBlobReadLinks.For</c> stays valid at most. Links are issued in windows
    /// of half this span: a blob keeps one link through a window, and a link handed out always has at
    /// least half of the span left.
    /// </summary>
    public TimeSpan ReadLinkLifetime { get; init; } = TimeSpan.FromHours(2);

    /// <summary>How long a link from <c>IBlobReadLinks.ForSharing</c> stays valid.</summary>
    public TimeSpan SharedLinkLifetime { get; init; } = TimeSpan.FromDays(365);

    public string UsageMetadataKey { get; init; } = "inUse";
}
