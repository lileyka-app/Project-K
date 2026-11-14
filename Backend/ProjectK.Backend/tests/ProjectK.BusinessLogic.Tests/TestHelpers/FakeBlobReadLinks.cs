using ProjectK.BusinessLogic.MappingProfiles.Resolvers;
using ProjectK.Common.Interfaces.Modules.InfrastructureModule;

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

    /// <summary>
    /// For <c>MapperConfiguration.ConstructServicesUsing</c>: builds the photo resolvers over this
    /// fake, and anything else the profiles ask for with its parameterless constructor.
    /// </summary>
    public static object Resolvers(Type type)
    {
        if (type == typeof(ProfilePhotoUrlResolver)) return new ProfilePhotoUrlResolver(Instance);
        if (type == typeof(MemberListItemPhotoUrlResolver)) return new MemberListItemPhotoUrlResolver(Instance);
        if (type == typeof(MemberLookupPhotoUrlResolver)) return new MemberLookupPhotoUrlResolver(Instance);
        if (type == typeof(MemberSummaryLookupPhotoUrlResolver)) return new MemberSummaryLookupPhotoUrlResolver(Instance);
        if (type == typeof(GroupSilhouetteUrlResolver)) return new GroupSilhouetteUrlResolver(Instance);
        return Activator.CreateInstance(type)!;
    }
}
