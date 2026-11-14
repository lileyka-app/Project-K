namespace ProjectK.Common.Enums.InfrastructureModule;

public enum BlobUploadProcessingMode
{
    CompressToJpeg,
    EncodeAsPng,

    /// <summary>Kept byte for byte when it starts with the PDF signature, and stored as a download.</summary>
    KeepPdf
}
