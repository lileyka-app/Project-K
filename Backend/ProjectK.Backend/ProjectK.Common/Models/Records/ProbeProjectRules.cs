namespace ProjectK.Common.Models.Records;

/// <summary>
/// The limits of the third probe's file and projects. Numbers, not styling: inline pictures and
/// attached посвідки must not turn blob storage into a dump, so the server holds them and the
/// clients only repeat them.
/// </summary>
public static class ProbeProjectRules
{
    /// <summary>The programme asks for five projects, one from each area.</summary>
    public const int MaxMainProjects = 5;

    /// <summary>One more, written out in reserve and swapped in when a main one falls through.</summary>
    public const int MaxSpareProjects = 1;

    public const int MaxProjects = MaxMainProjects + MaxSpareProjects;

    /// <summary>«Два проєкти можуть бути спільні з іншими третьопробниками.»</summary>
    public const int MaxSharedMainProjects = 2;

    public const int MaxImagesPerProject = 5;

    /// <summary>One picture as it arrives, before it is re-encoded: 1 MB.</summary>
    public const long MaxImageBytes = 1024 * 1024;

    /// <summary>
    /// Everything one person may keep in storage for their third probe, pictures and посвідка
    /// together: 10 MB, counted by what was uploaded. Ten full-size pictures fill it; a
    /// probe needs a few per project, and blob storage is paid for.
    /// </summary>
    public const long MaxStoredBytes = 10 * 1024 * 1024;

    /// <summary>The посвідка of the camp, a picture or a PDF: 2 MB.</summary>
    public const long MaxCertificateBytes = 2 * 1024 * 1024;

    /// <summary>Each of a project's texts (description, goal, plan, results), pictures as short tokens.</summary>
    public const int MaxTextLength = 10_000;

    public const int MaxResumeLength = 10_000;
    public const int MaxShortTextLength = 200;
    public const int MaxCommentLength = 2_000;

    /// <summary>The probe's place in its edition (знак прихильника is 0).</summary>
    public const int ProbeOrder = 3;

    /// <summary>A youth's programme ends on their 18th birthday, and the third probe with it.</summary>
    public const int YouthEndsAtAge = 18;

    /// <summary>The вмілості the probe asks for, one of them for тіловиховання.</summary>
    public const int RequiredBadges = 4;

    /// <summary>How a picture is written into the Markdown: <c>![…](project-image:{key})</c>.</summary>
    public const string ImageScheme = "project-image:";
}
