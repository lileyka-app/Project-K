namespace ProjectK.Common.Dtos.KurinModule.Requests;

/// <summary>Body of <c>PUT api/kurin/{kurinKey}/card-fields</c>: the optional fields the kurin hides.</summary>
public sealed class SetKurinCardFieldsRequest
{
    public List<string>? Hidden { get; set; }
}
