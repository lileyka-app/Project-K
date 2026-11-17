namespace ProjectK.Common.Records.Shared;

/// <summary>A stored file: its blob, a read link, and how many bytes it takes in storage after re-encoding.</summary>
public sealed record PhotoUploadResult(string BlobName, string Url, long Bytes = 0);
