namespace ProjectK.Common.Dtos.KurinModule.Requests;

/// <summary>Body of <c>POST api/kurin/{kurinKey}/branch-request</c>: a null number leaves that гілка out.</summary>
public sealed class SubmitKurinBranchRequest
{
    public int? UspNumber { get; set; }
    public int? UpsNumber { get; set; }
    public string? Note { get; set; }
}
