namespace ProjectK.Common.Dtos.KurinModule.Requests;

/// <summary>Body of <c>PUT api/kurin/{kurinKey}/tags/{tagKey}/members</c>: everyone who should carry it now.</summary>
public class SetTagMembersRequest
{
    public List<Guid> MemberKeys { get; set; } = [];
}
