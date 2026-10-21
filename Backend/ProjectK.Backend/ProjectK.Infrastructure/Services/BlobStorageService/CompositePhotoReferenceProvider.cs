namespace ProjectK.Infrastructure.Services.BlobStorageService;

public sealed class CompositePhotoReferenceProvider : IPhotoReferenceProvider
{
    private readonly IReadOnlyCollection<IPhotoReferenceProvider> _providers;

    public CompositePhotoReferenceProvider(IEnumerable<IPhotoReferenceProvider> providers)
    {
        _providers = providers.ToList();
    }

    public async Task<IReadOnlyCollection<string>> GetAllReferencedBlobNamesAsync(CancellationToken cancellationToken)
    {
        var references = new HashSet<string>(StringComparer.Ordinal);

        foreach (var provider in _providers)
        {
            var providerReferences = await provider.GetAllReferencedBlobNamesAsync(cancellationToken)
                .ConfigureAwait(false);

            references.UnionWith(providerReferences.Where(blobName => !string.IsNullOrWhiteSpace(blobName)));
        }

        return references.ToList();
    }
}
