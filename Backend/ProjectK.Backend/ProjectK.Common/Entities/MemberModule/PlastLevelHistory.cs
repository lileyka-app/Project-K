using ProjectK.Common.Enums.Shared;

namespace ProjectK.Common.Entities.MemberModule;

public class PlastLevelHistory
{
    public Guid PlastLevelHistoryKey { get; set; }

    public Guid MemberKey { get; set; }

    /// <summary>
    /// Where the level was reached. Nullable because entries recorded before memberships existed
    /// carry no such context — the level is the person's either way.
    /// </summary>
    public Guid? KurinKey { get; set; }
    public Member Member { get; set; }

    public PlastLevel PlastLevel { get; set; }

    /// <summary>
    /// A пластунка's ступінь rather than a пластун's: the same step, its own name and відзнака in the
    /// catalogue. Chosen when the ступінь is recorded; the card keeps no sex of its own.
    /// </summary>
    public bool IsPlastunka { get; set; }

    public DateOnly DateAchieved { get; set; }

    /// A senior ступінь a юнак entered on their own card — the step out of УПЮ — waiting for the Звʼязковий of a kurin they
    /// A senior ступінь the person entered on their own card, waiting for the Звʼязковий of a kurin they
    /// stand in. Until confirmed it counts for nothing — not the улад, not the highest ступінь, not
    /// <see cref="Member.LatestPlastLevel"/> — and the card shows it marked as waiting.
    /// </summary>
    public bool AwaitsConfirmation { get; set; }

    /// <summary>Who confirmed a ступінь that waited; null for one that never waited.</summary>
    public Guid? ConfirmedByUserKey { get; set; }

    /// <summary>When a ступінь that waited was confirmed; null for one that never waited.</summary>
    public DateTime? ConfirmedAtUtc { get; set; }
}
