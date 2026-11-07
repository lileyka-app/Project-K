namespace ProjectK.Common.Entities.KurinModule.Tags;

/// <summary>
/// A tag on a person. Keyed by the person, not by their membership: the tag is the kurin's, so it
/// dies with the tag, and a person who leaves the kurin simply stops being listed in it.
/// </summary>
public class MemberTag
{
    public Guid KurinTagKey { get; set; }
    public KurinTag? Tag { get; set; }
    public Guid MemberKey { get; set; }
    public DateTime AssignedAtUtc { get; set; } = DateTime.UtcNow;
}
