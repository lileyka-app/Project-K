namespace ProjectK.Common.Entities.KurinModule.Tags;

/// <summary>
/// The kurin's tag switches, kept apart from <see cref="Kurin"/>. A kurin without a row has the
/// defaults of <see cref="Models.Tags.KurinTagRules"/>. Who sees tags is not here: that is the
/// kurin's «Права й видимість» (<c>KurinCapability.SeeTags</c>).
/// </summary>
public class KurinTagSettings
{
    public Guid KurinKey { get; set; }

    /// <summary>Whether the kurin uses tags; null follows its branch.</summary>
    public bool? Enabled { get; set; }

    /// <summary>Whether a person sees the tags on themselves, whatever the policy says about others.</summary>
    public bool OwnTagsVisible { get; set; } = true;
}
