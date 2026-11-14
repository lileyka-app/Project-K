using ProjectK.Common.Models.Enums;

namespace ProjectK.Common.Dtos.ProbesAndBadgesModule.Requests;

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
