using ProjectK.Common.Enums.MemberModule;


namespace ProjectK.Common.Dtos.KurinModule.Requests;

/// <summary>Body of <c>PUT api/kurin/{kurinKey}/card-sharing</c>.</summary>
public sealed class SetKurinCardSharingRequest
{
    public bool MinorsMayShare { get; set; }
    public CardField MinorFields { get; set; }
    public bool MinorsMayLink { get; set; }
}
