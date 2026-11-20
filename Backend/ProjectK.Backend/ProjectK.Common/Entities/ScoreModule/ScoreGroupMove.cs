using ProjectK.Common.Entities.Shared;

namespace ProjectK.Common.Entities.ScoreModule;

/// <summary>
/// A person moved between гуртки of a kurin. A membership remembers only where it stands now, and a
/// гурток keeps the points earned while the person was in it, so the score module writes the move down
/// as it happens. With none, a person has been in their current гурток all along.
/// </summary>
public class ScoreGroupMove : Entity
{
    public Guid ScoreGroupMoveKey { get; set; } = Guid.NewGuid();
    public Guid KurinKey { get; set; }
    public Guid MembershipKey { get; set; }

    public Guid? FromGroupKey { get; set; }
    public Guid? ToGroupKey { get; set; }

    public DateTime MovedAtUtc { get; set; }
}
