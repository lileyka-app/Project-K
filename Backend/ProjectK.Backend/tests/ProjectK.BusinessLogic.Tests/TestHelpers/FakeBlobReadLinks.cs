using ProjectK.Common.Contracts.NotificationsModule;

namespace ProjectK.BusinessLogic.Tests.TestHelpers;

/// <summary>
/// Read links without a storage account: the blob name on a fixed host with a visible marker, so a
/// test can tell a signed link from a raw blob name.
/// </summary>
public sealed class FakeBlobReadLinks : IBlobReadLinks
{
    public static readonly FakeBlobReadLinks Instance = new();

    public string? For(string? blobName)
        => string.IsNullOrWhiteSpace(blobName) ? null : $"https://cdn.test/{blobName}?sig=read";

    public string? ForSharing(string? blobName)
        => string.IsNullOrWhiteSpace(blobName) ? null : $"https://cdn.test/{blobName}?sig=shared";
}
