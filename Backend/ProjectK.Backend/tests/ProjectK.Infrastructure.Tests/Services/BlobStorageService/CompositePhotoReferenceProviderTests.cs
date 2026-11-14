using ProjectK.Infrastructure.Services.Blobs;

namespace ProjectK.Infrastructure.Tests.Services.BlobStorageService;

public class CompositePhotoReferenceProviderTests
{
    [Fact]
    public async Task GetAllReferencedBlobNamesAsync_ShouldMergeDistinctReferences()
    {
        // Arrange
        var provider = new CompositePhotoReferenceReader(
        [
            new StaticPhotoReferenceProvider("member-photos/2026/05/27/a.jpg", "photos/legacy.jpg"),
            new StaticPhotoReferenceProvider("member-photos/2026/05/27/a.jpg", "group-silhouettes/2026/05/27/b.png")
        ]);

        // Act
        var result = await provider.GetAllReferencedBlobNamesAsync(CancellationToken.None);

        // Assert
        Assert.Equal(
            [
                "member-photos/2026/05/27/a.jpg",
                "photos/legacy.jpg",
                "group-silhouettes/2026/05/27/b.png"
            ],
            result);
    }

    private sealed class StaticPhotoReferenceProvider : IPhotoReferenceReader
    {
        private readonly IReadOnlyCollection<string> _references;

        public StaticPhotoReferenceProvider(params string[] references)
        {
            _references = references;
        }

        public Task<IReadOnlyCollection<string>> GetAllReferencedBlobNamesAsync(CancellationToken cancellationToken)
            => Task.FromResult(_references);
    }
}
