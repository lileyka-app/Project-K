namespace ProjectK.Infrastructure.Services.Blobs;

public interface IPhotoReferenceReader
{
    Task<IReadOnlyCollection<string>> GetAllReferencedBlobNamesAsync(CancellationToken cancellationToken);
}
