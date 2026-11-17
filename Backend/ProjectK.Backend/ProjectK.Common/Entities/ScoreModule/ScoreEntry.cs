using ProjectK.Common.Entities.Shared;

namespace ProjectK.Common.Entities.ScoreModule;

/// <summary>
/// Points given by hand, plus or minus, to a person (<see cref="MembershipKey"/>) or to a whole гурток
/// (<see cref="GroupKey"/>) — exactly one of the two. Either a position from the kurin's list
/// (<see cref="ScoreItemKey"/>) or a free amount with a <see cref="Reason"/>.
/// <para>
/// A position given at an event is unique for that person or гурток there; a free entry is not —
/// a курінний суддя may well add to what the гуртковий gave — so it is the screen that shows what is
/// already there, not the database that refuses. Nothing is hard-deleted.
/// </para>
/// </summary>
public class ScoreEntry : Entity
{
    public Guid ScoreEntryKey { get; set; } = Guid.NewGuid();
    public Guid KurinKey { get; set; }

    public Guid? MembershipKey { get; set; }
    public Guid? GroupKey { get; set; }

    public Guid? ScoreItemKey { get; set; }

    /// <summary>
    /// Copied from the position when it is given, so a later change to the list does not rewrite
    /// what was earned; the free amount otherwise.
    /// </summary>
    public int Points { get; set; }

    public string? Reason { get; set; }

    /// <summary>The event it was given at, if any — and which occurrence of it.</summary>
    public Guid? AgendaItemKey { get; set; }
    public DateTime? OccurrenceStartUtc { get; set; }

    public DateOnly OccurredOn { get; set; }

    public Guid? CreatedByUserKey { get; set; }

    public DateTime? DeletedAtUtc { get; set; }
    public Guid? DeletedByUserKey { get; set; }

    public bool IsDeleted => DeletedAtUtc.HasValue;
}
