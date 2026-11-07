namespace ProjectK.Common.Entities.KurinModule.Tags;

/// <summary>
/// A tag a kurin defines for itself and puts on its people: a level, a belonging, a rank of its own.
/// In a senior kurin it is how people are told apart in place of гуртки; in a УПЮ kurin it only adds to them.
/// </summary>
public class KurinTag : Entity
{
    public Guid KurinTagKey { get; set; } = Guid.NewGuid();
    public Guid KurinKey { get; set; }
    public Kurin? Kurin { get; set; }

    /// <summary>Unique within the kurin, compared without regard to case.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>One of <see cref="Models.Tags.KurinTagRules.Palette"/>: the colour is data, not a style.</summary>
    public string ColorHex { get; set; } = string.Empty;

    public ICollection<MemberTag> Members { get; set; } = new List<MemberTag>();
}
