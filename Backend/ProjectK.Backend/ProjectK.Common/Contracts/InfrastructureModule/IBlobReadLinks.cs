namespace ProjectK.Common.Contracts.InfrastructureModule;

/// <summary>
/// Addresses for reading stored photos. The container is private, so every address is a read-only
/// link signed for that one blob; nothing in it can be listed, guessed or rewritten.
/// </summary>
public interface IBlobReadLinks
{
    /// <summary>
    /// A short-lived link for showing <paramref name="blobName"/> in the app, or null for an empty
    /// name. The same blob keeps the same link for a while, so the browser cache keeps working.
    /// </summary>
    string? For(string? blobName);

    /// <summary>
    /// A long-lived link for a picture that leaves the app on purpose, such as a screenshot embedded
    /// in a GitHub issue.
    /// </summary>
    string? ForSharing(string? blobName);
}
