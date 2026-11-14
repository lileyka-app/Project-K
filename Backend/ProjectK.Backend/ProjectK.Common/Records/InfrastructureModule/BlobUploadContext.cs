using ProjectK.Common.Enums.InfrastructureModule;
using ProjectK.Common.Rules.InfrastructureModule;

namespace ProjectK.Common.Records.InfrastructureModule;

public sealed record BlobUploadContext(
    string Folder,
    BlobUploadProcessingMode ProcessingMode,
    string ContentType)
{
    public static BlobUploadContext MemberPhoto { get; } =
        new(BlobUploadFolders.MemberPhotos, BlobUploadProcessingMode.CompressToJpeg, "image/jpeg");

    public static BlobUploadContext GroupSilhouette { get; } =
        new(BlobUploadFolders.GroupSilhouettes, BlobUploadProcessingMode.EncodeAsPng, "image/png");

    /// <summary>A senior kurin's emblem: kept as PNG like a silhouette, so its transparency survives.</summary>
    public static BlobUploadContext KurinEmblem { get; } =
        new(BlobUploadFolders.KurinEmblems, BlobUploadProcessingMode.EncodeAsPng, "image/png");

    /// <summary>A picture inside a third-probe project. Resized like a photo: it is read on a phone.</summary>
    public static BlobUploadContext ProbeProjectImage { get; } =
        new(BlobUploadFolders.ProbeProjectImages, BlobUploadProcessingMode.CompressToJpeg, "image/jpeg");

    /// <summary>The посвідка of a third-prober's camp as a photo or scan.</summary>
    public static BlobUploadContext ThirdProbeCertificateImage { get; } =
        new(BlobUploadFolders.ThirdProbeCertificates, BlobUploadProcessingMode.CompressToJpeg, "image/jpeg");

    /// <summary>The same посвідка as a PDF.</summary>
    public static BlobUploadContext ThirdProbeCertificatePdf { get; } =
        new(BlobUploadFolders.ThirdProbeCertificates, BlobUploadProcessingMode.KeepPdf, "application/pdf");

    /// <summary>
    /// A screenshot attached to a problem report. Re-encoded like everything else, so nothing but
    /// a decoded image is stored; never resized, a screenshot is read for detail.
    /// </summary>
    public static BlobUploadContext FeedbackScreenshot { get; } =
        new(BlobUploadFolders.FeedbackScreenshots, BlobUploadProcessingMode.EncodeAsPng, "image/png");
}
