using ProjectK.Common.Records.Shared;

namespace ProjectK.Common.Contracts.Shared;

public interface IPhotoService
{
    // Stream overloads let callers feed the request/file stream straight into image processing
    // without first buffering the whole original file into a byte[] in memory.
    Task<PhotoUploadResult> UploadPhotoAsync(Stream photoStream, string fileName, CancellationToken cancellationToken);
    Task<PhotoUploadResult> UploadPhotoAsync(Stream photoStream, string fileName, BlobUploadContext uploadContext, CancellationToken cancellationToken);
    Task<PhotoUploadResult> UploadPhotoAsync(byte[] photoBytes, string fileName, CancellationToken cancellationToken);
    Task<PhotoUploadResult> UploadPhotoAsync(byte[] photoBytes, string fileName, BlobUploadContext uploadContext, CancellationToken cancellationToken);
    Task<bool> DeletePhotoAsync(string photoUrl, CancellationToken cancellationToken);

    /// <summary>
    /// Creates the container if it is missing and closes public access on one that an older version
    /// left public. Run at startup, so a deploy shuts the old anonymous URLs without waiting for an upload.
    /// </summary>
    Task PrepareStorageAsync(CancellationToken cancellationToken);
}
