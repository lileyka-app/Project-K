namespace ProjectK.Infrastructure.Services.Blobs;

internal sealed record PreparedBlobUpload(byte[] ProcessedBytes, string FinalExtension, string ContentType);
