namespace ProjectK.Common.Dtos.KurinModule.Requests;

/// <summary>Body of <c>PUT api/kurin/{kurinKey}/krai</c>.</summary>
public sealed class SetKurinKraiRequest
{
    public string KraiCode { get; set; } = string.Empty;
}
