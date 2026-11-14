namespace ProjectK.Infrastructure.Services.Blobs;

public interface IPhotoReferenceProvider
{
    Task<IReadOnlyCollection<string>> GetAllReferencedBlobNamesAsync(CancellationToken cancellationToken);
}
