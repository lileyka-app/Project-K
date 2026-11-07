namespace ProjectK.Common.Entities.KurinModule;

/// <summary>
/// One stretch a membership spent in one гурток. A membership remembers only its current гурток,
/// so the archive reads the earlier ones from here.
/// <para>
/// Nobody writes these by hand: <c>AppDbContext.SaveChangesAsync</c> opens and closes them from
/// every change to <see cref="Membership.GroupKey"/> and <see cref="Membership.LeftAtUtc"/>, so no
/// path that moves a person can forget to. The record starts with release 1.4; a stretch already
/// running then has no known start.
/// </para>
/// </summary>
public class MembershipGroupStint
{
    public Guid MembershipGroupStintKey { get; set; } = Guid.NewGuid();

    public Guid MembershipKey { get; set; }
    public Membership Membership { get; set; } = null!;

    /// <summary>A bare key: the stint outlives the гурток only as long as the archive keeps it.</summary>
    public Guid GroupKey { get; set; }

    /// <summary>Null for a stretch that was already running when the record began.</summary>
    public DateTime? FromUtc { get; set; }

    /// <summary>Null while the person is still in this гурток.</summary>
    public DateTime? ToUtc { get; set; }
}
