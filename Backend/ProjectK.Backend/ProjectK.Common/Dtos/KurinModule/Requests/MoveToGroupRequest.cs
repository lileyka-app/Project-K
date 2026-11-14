namespace ProjectK.Common.Dtos.KurinModule.Requests;

/// <summary>Body of <c>PUT api/kurin/{kurinKey}/memberships/{memberKey}/group</c>; a null гурток takes the person out of theirs.</summary>
public sealed class MoveToGroupRequest
{
    public Guid? GroupKey { get; set; }
}
