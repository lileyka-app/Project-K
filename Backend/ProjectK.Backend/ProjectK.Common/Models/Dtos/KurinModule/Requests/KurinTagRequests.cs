namespace ProjectK.Common.Models.Dtos.KurinModule.Requests;

/// <summary>Body of creating or renaming a tag.</summary>
public class UpsertKurinTagRequest
{
    public string Name { get; set; } = string.Empty;

    /// <summary>One of the palette's colours.</summary>
    public string ColorHex { get; set; } = string.Empty;
}

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

/// <summary>Body of <c>PUT api/kurin/{kurinKey}/tags/{tagKey}/members</c>: everyone who should carry it now.</summary>
public class SetTagMembersRequest
{
    public List<Guid> MemberKeys { get; set; } = [];
}

/// <summary>Body of <c>PUT api/kurin/{kurinKey}/tag-settings</c>.</summary>
public class SetKurinTagSettingsRequest
{
    public bool Enabled { get; set; }
    public bool OwnTagsVisible { get; set; } = true;
}
