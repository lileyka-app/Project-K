using ProjectK.Common.Entities.Shared;

namespace ProjectK.Common.Entities.ScoreModule;

/// <summary>
/// Points the КВ gave a youth among themselves: a book the Звʼязковий and the впорядники share and
/// nobody else opens. Never part of the table, never shown to the youth, never turned into a public
/// point — the КВ gives those the ordinary way. Soft-deleted, with its trail in <c>ScoreTrailEvent</c>.
/// </summary>
public class PrivateScoreEntry : Entity
{
    public Guid PrivateScoreEntryKey { get; set; } = Guid.NewGuid();
    public Guid KurinKey { get; set; }
    public Guid MembershipKey { get; set; }

    /// <summary>What it was for, from the КВ's own list; null for a note that fits no criterion.</summary>
    public Guid? PrivateScoreCriterionKey { get; set; }

    public int Points { get; set; }
    public string? Note { get; set; }
    public DateOnly OccurredOn { get; set; }

    public Guid? CreatedByUserKey { get; set; }

    public DateTime? DeletedAtUtc { get; set; }
    public Guid? DeletedByUserKey { get; set; }

    public bool IsDeleted => DeletedAtUtc.HasValue;
}
