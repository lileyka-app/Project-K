namespace ProjectK.Common.Contracts.KurinModule;

/// <summary>
/// Fetches the image bytes the PDF embeds — group silhouettes and member photos.
/// <para>
/// An interface because the blob client lives in Infrastructure while the report is assembled in the
/// business layer, and a missing or unreadable image must never fail the report.
/// </para>
/// </summary>
public interface IKurinReportMedia
{
    /// <summary>Returns the blob's bytes, or <c>null</c> when it is absent or cannot be read.</summary>
    Task<byte[]?> TryDownloadAsync(string? blobName, CancellationToken cancellationToken = default);
}
