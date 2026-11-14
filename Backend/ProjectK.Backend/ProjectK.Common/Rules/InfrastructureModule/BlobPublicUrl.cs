namespace ProjectK.Common.Rules.InfrastructureModule;

/// <summary>
/// Builds the public address of a stored blob. The single place that does so.
/// <para>
/// Blob names are foldered — <c>{folder}/{yyyy}/{MM}/{guid}{ext}</c> — so each segment is escaped
/// separately. Escaping the whole name would turn the separators into <c>%2F</c> and address a
/// different blob; two of the four hand-rolled builders this replaced did exactly that.
/// </para>
/// </summary>
public static class BlobPublicUrl
{
    /// <summary>
    /// The public URL for <paramref name="blobName"/>, or <paramref name="fallback"/> when no public
    /// base URL is configured. Returns null for an empty blob name.
    /// </summary>
    public static string? Build(string? publicBaseUrl, string? blobName, string? fallback = null)
    {
        if (string.IsNullOrWhiteSpace(blobName))
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(publicBaseUrl))
        {
            return fallback ?? blobName;
        }

        return $"{publicBaseUrl.TrimEnd('/')}/{EncodePath(blobName)}";
    }

    private static string EncodePath(string blobName) =>
        string.Join("/", blobName
            .Split('/', StringSplitOptions.RemoveEmptyEntries)
            .Select(Uri.EscapeDataString));
}
