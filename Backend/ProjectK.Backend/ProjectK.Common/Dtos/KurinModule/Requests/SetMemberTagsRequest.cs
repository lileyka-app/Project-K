namespace ProjectK.Common.Dtos.KurinModule.Requests;

/// <summary>Body of <c>PUT api/member/{memberKey}/tags</c>: every tag the person should carry now.</summary>
public class SetMemberTagsRequest
{
    public List<Guid> TagKeys { get; set; } = [];

    /// <summary>
    /// Whose tags: only an administrator who has not chosen a kurin names it. Anyone acting in a kurin
    /// sets that kurin's tags, whatever is sent here.
    /// </summary>
    public Guid? KurinKey { get; set; }
}
