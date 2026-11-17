namespace ProjectK.Common.Dtos.KurinModule.Requests;

/// <summary>Body of <c>POST api/kurin</c>.</summary>
public sealed class CreateKurinRequest
{
    public int Number { get; set; }
    public string? KraiCode { get; set; }
}
