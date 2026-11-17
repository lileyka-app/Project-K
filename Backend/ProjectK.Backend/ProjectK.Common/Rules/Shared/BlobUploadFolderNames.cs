namespace ProjectK.Common.Rules.Shared;

public static class BlobUploadFolderNames
{
    public const string MemberPhotos = "member-photos";
    public const string GroupSilhouettes = "group-silhouettes";
    public const string KurinEmblems = "kurin-emblems";
    public const string ProbeProjectImages = "probe-project-images";
    public const string ThirdProbeCertificates = "third-probe-certificates";

    /// <summary>
    /// Not a scenario folder on purpose: nothing in the database references these blobs, and the
    /// orphan cleanup only sweeps <see cref="ScenarioFolders"/>, so an issue keeps its pictures.
    /// </summary>
    public const string FeedbackScreenshots = "feedback-screenshots";

    public static IReadOnlyCollection<string> ScenarioFolders { get; } =
    [
        MemberPhotos,
        GroupSilhouettes,
        KurinEmblems,
        ProbeProjectImages,
        ThirdProbeCertificates
    ];
}
