using ProjectK.Common.Models.Enums;

namespace ProjectK.Common.Models.Dtos.ProbesAndBadgesModule.Requests;

/// <summary>A third-probe project, laid out as the УПЮ template asks.</summary>
public class SaveProbeProjectRequest
{
    public string? Title { get; set; }
    public ProbeProjectArea? Area { get; set; }
    public List<ProbeProjectReach>? Reach { get; set; }
    public bool IsShared { get; set; }
    public string? SharedWith { get; set; }
    public DateOnly? StartsOn { get; set; }
    public DateOnly? EndsOn { get; set; }
    public string? Location { get; set; }
    public string? EventLeads { get; set; }
    public string? CoordinatorName { get; set; }
    public string? CoordinatorContact { get; set; }

    /// <summary>Markdown, like Goal, Plan and Results; pictures as <c>![…](project-image:{key})</c>.</summary>
    public string? Description { get; set; }
    public string? Goal { get; set; }
    public string? Plan { get; set; }
    public string? Results { get; set; }
}

/// <summary>The third-probe file as the person writes it; confirmations and stages go their own way.</summary>
public class SaveThirdProbeFileRequest
{
    public string? MentorZvyazkovyi { get; set; }
    public string? MentorVporiadnyk { get; set; }
    public string? MentorReferent { get; set; }
    public DateOnly? ReflectionOn { get; set; }
    public string? ReflectionWith { get; set; }
    public ThirdProbeCampKind? CampKind { get; set; }
    public string? CampName { get; set; }
    public DateOnly? CampFrom { get; set; }
    public DateOnly? CampTo { get; set; }
    public string? PhysicalBadgeId { get; set; }
    public string? Resume { get; set; }
}

/// <summary>The referent's stages, each the day it happened; a null clears it.</summary>
public class SetThirdProbeStagesRequest
{
    public DateOnly? MotivationLetterOn { get; set; }
    public DateOnly? SelfReflectionOn { get; set; }
    public DateOnly? ApplicationSubmittedOn { get; set; }
    public DateOnly? OpenedOn { get; set; }
    public DateOnly? CompletedOn { get; set; }
    public DateOnly? NamedOn { get; set; }
    public DateOnly? ArchivedOn { get; set; }
}

/// <summary>A project marked done, with what its coordinator said.</summary>
public class CompleteProbeProjectRequest
{
    public string? CoordinatorComment { get; set; }
}
