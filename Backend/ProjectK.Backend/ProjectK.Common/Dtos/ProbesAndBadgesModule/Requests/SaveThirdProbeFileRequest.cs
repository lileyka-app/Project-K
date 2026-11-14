using ProjectK.Common.Models.Enums;

namespace ProjectK.Common.Dtos.ProbesAndBadgesModule.Requests;

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
