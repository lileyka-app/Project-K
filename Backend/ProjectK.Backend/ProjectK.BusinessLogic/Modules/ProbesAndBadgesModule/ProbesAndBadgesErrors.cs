using ProjectK.Common.Enums.Shared;
using ProjectK.Common.Records.Shared;

namespace ProjectK.BusinessLogic.Modules.ProbesAndBadgesModule;

/// <summary>
/// The codes a проба, вмілість, catalogue or third-probe request fails with. The clients word them
/// themselves (<c>REFUSALS</c>, <c>FILE_REFUSALS</c> on the web, the probe sheets on the phone).
/// </summary>
public static class ProbesAndBadgesErrors
{
    public const string Unauthorized = "Unauthorized";
    public const string KurinNotFound = "KurinNotFound";
    public const string MemberNotFound = "MemberNotFound";

    /// <summary>The database took none of the change.</summary>
    public const string NotSaved = "ProgressNotSaved";

    // Catalogue
    public const string ProbeNotFound = "ProbeNotFound";
    public const string BadgeNotFound = "BadgeNotFound";
    public const string AwardImageNotFound = "AwardImageNotFound";

    // Вмілості
    public const string BadgeIdRequired = "BadgeIdRequired";
    public const string BadgeProgressNotFound = "BadgeProgressNotFound";

    /// <summary>The вмілість is not in the kurin's catalogue for its край.</summary>
    public const string BadgeNotOffered = "BadgeNotOffered";

    public const string BadgeAlreadyConfirmed = "BadgeAlreadyConfirmed";

    /// <summary>Neither handed in for review nor confirmed: there is nothing to decide.</summary>
    public const string BadgeNotReviewable = "BadgeNotReviewable";

    public const string BadgeReviewNotAllowed = "BadgeReviewNotAllowed";

    // Проби
    public const string ProbeIdRequired = "ProbeIdRequired";
    public const string ProbePointRequired = "ProbePointRequired";

    /// <summary>No probe id, or a status a проба cannot be moved to by hand.</summary>
    public const string InvalidProbeStatus = "InvalidProbeStatus";

    public const string ProbeUnsignNotAllowed = "ProbeUnsignNotAllowed";
    public const string ProbeNotInLine = "ProbeNotInLine";
    public const string ProbeWaitsForPrevious = "ProbeWaitsForPrevious";

    // Третя проба
    public const string NotProjectProbe = "NotProjectProbe";

    /// <summary>The caller may neither write this person's third probe nor sign it.</summary>
    public const string ProbeProjectEditNotAllowed = "ProbeProjectEditNotAllowed";

    public const string ProjectNotFound = "ProjectNotFound";
    public const string TitleRequired = "TitleRequired";
    public const string FieldTooLong = "FieldTooLong";
    public const string DescriptionTooLong = "DescriptionTooLong";
    public const string UnknownArea = "UnknownArea";
    public const string UnknownCampKind = "UnknownCampKind";
    public const string EndsBeforeStart = "EndsBeforeStart";

    /// <summary>A text shows a picture it may not: one not uploaded into the project, or any in the résumé.</summary>
    public const string ForeignImage = "ForeignImage";

    public const string TooManyImages = "TooManyImages";
    public const string TooManyProjects = "TooManyProjects";
    public const string TooManyShared = "TooManyShared";
    public const string ImageTooLarge = "ImageTooLarge";
    public const string InvalidImageContent = "InvalidImageContent";
    /// <summary>An upload came without a file, or with an empty one.</summary>
    public const string MissingFile = "MissingFile";

    public const string CertificateTooLarge = "CertificateTooLarge";
    public const string InvalidCertificate = "InvalidCertificate";

    /// <summary>The person's pictures and посвідка would pass <c>ProbeProjectRules.MaxStoredBytes</c>.</summary>
    public const string StorageFull = "StorageFull";

    public const string BadgeNotHeld = "BadgeNotHeld";
    public const string NothingToConfirm = "NothingToConfirm";
    public const string StagesOutOfOrder = "StagesOutOfOrder";

    /// <summary>The account stands behind no member record, so there is no «me» to read.</summary>
    public const string NotAMember = "NotAMember";

    public static ServiceResult<T> NoMember<T>() =>
        ServiceResult<T>.Failure(ResultType.Forbidden, NotAMember, "The account has no member record.");
}
