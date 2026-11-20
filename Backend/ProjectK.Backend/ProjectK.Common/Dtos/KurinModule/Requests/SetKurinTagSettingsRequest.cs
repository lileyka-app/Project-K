namespace ProjectK.Common.Dtos.KurinModule.Requests;

/// <summary>Body of <c>PUT api/kurin/{kurinKey}/tag-settings</c>.</summary>
public class SetKurinTagSettingsRequest
{
    public bool Enabled { get; set; }
    public bool OwnTagsVisible { get; set; } = true;
}
