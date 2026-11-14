namespace ProjectK.Common.Dtos.ProbesAndBadgesModule.Requests;

/// <summary>A project marked done, with what its coordinator said.</summary>
public class CompleteProbeProjectRequest
{
    public string? CoordinatorComment { get; set; }
}
