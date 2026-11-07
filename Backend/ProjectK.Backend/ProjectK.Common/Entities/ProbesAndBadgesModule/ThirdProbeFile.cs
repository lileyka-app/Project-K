using ProjectK.Common.Models.Enums;

namespace ProjectK.Common.Entities.ProbesAndBadgesModule;

/// <summary>
/// Everything of the third probe that is not a project: the mentors, the reflection with the
/// Звʼязковий, the camp with its посвідка, the вмілість that covers тіловиховання, the пластове
/// резюме — and the stages the referent of the third probe tracks from the motivation letter to the
/// archive. One per person and probe; created on the first write.
/// </summary>
public class ThirdProbeFile : Entity
{
    public Guid ThirdProbeFileKey { get; set; } = Guid.NewGuid();
    public Guid MemberKey { get; set; }
    public string ProbeId { get; set; } = string.Empty;

    /// <summary>«Ментори»: who guides the probe, by the role they guide it in.</summary>
    public string? MentorZvyazkovyi { get; set; }
    public string? MentorVporiadnyk { get; set; }
    public string? MentorReferent { get; set; }

    /// <summary>The reflection «Що для мене означає бути пластуном» with the Звʼязковий or referent.</summary>
    public DateOnly? ReflectionOn { get; set; }
    public string? ReflectionWith { get; set; }
    public DateTime? ReflectionConfirmedAtUtc { get; set; }
    public Guid? ReflectionConfirmedByUserKey { get; set; }
    public string? ReflectionConfirmedByName { get; set; }

    /// <summary>
    /// The camp, as the person writes it down. Whether the край counts it is the край's decision,
    /// not Lilejka's: there is no confirmation here, only what the camp was.
    /// </summary>
    public ThirdProbeCampKind? CampKind { get; set; }
    public string? CampName { get; set; }
    public DateOnly? CampFrom { get; set; }
    public DateOnly? CampTo { get; set; }

    /// <summary>The посвідка: a picture or a PDF, kept alive by this column alone.</summary>
    public string? CampCertificateBlobName { get; set; }
    public string? CampCertificateContentType { get; set; }
    public string? CampCertificateFileName { get; set; }

    /// <summary>The bytes the посвідка took on upload, counted against the person's storage.</summary>
    public long CampCertificateBytes { get; set; }

    /// <summary>The catalogue id of the вмілість that shows фізична вправність або тіловиховання.</summary>
    public string? PhysicalBadgeId { get; set; }

    /// <summary>«Пластове резюме», in Markdown without pictures.</summary>
    public string Resume { get; set; } = string.Empty;

    /// <summary>
    /// The stages a referent of the third probe tracks, each the day it happened. Opening: the
    /// motivation letter (a plan of the probe; asked of a 15-year-old), the self-reflection, the
    /// application sent to the край, the day the probe opened. Closing: done, named, archived.
    /// </summary>
    public DateOnly? MotivationLetterOn { get; set; }
    public DateOnly? SelfReflectionOn { get; set; }
    public DateOnly? ApplicationSubmittedOn { get; set; }
    public DateOnly? OpenedOn { get; set; }
    public DateOnly? CompletedOn { get; set; }
    public DateOnly? NamedOn { get; set; }
    public DateOnly? ArchivedOn { get; set; }

    public DateTime? StagesUpdatedAtUtc { get; set; }
    public string? StagesUpdatedByName { get; set; }

    public Guid? UpdatedByUserKey { get; set; }
    public string? UpdatedByName { get; set; }
}
