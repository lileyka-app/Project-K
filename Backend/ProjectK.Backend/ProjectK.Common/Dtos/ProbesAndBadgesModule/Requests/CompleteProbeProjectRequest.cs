namespace ProjectK.Common.Dtos.ProbesAndBadgesModule.Requests;

/// <summary>A project marked done, with what its комендант said.</summary>
public class CompleteProbeProjectRequest
{
    public string? CommandantComment { get; set; }
}
