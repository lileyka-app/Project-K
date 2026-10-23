namespace ProjectK.Common.Models.Enums;

/// <summary>
/// The order a person passes the ступені in, and which of them a kurin of a given branch
/// normally records. Order lives here rather than in the enum because the enum's numbers are
/// storage and cannot be reordered — see <see cref="PlastLevel"/>.
/// </summary>
public static class PlastLadder
{
    /// <summary>Every ступінь, in the order it is reached.</summary>
    public static readonly IReadOnlyList<PlastLevel> InOrder =
    [
        PlastLevel.Entry,
        PlastLevel.Prykhylnyk,
        PlastLevel.Uchasnyk,
        PlastLevel.Rozviduvach,
        PlastLevel.Skob,
        PlastLevel.HetmanskiySkob,
        PlastLevel.Starshoplastun,
        PlastLevel.Senior,
        PlastLevel.SeniorPratsi,
        PlastLevel.SeniorDovirja,
        PlastLevel.SeniorKerivnytstva
    ];

    private static readonly IReadOnlyList<PlastLevel> ThroughYouth =
    [
        PlastLevel.Entry,
        PlastLevel.Prykhylnyk,
        PlastLevel.Uchasnyk,
        PlastLevel.Rozviduvach,
        PlastLevel.Skob,
        PlastLevel.HetmanskiySkob
    ];

    private static readonly IReadOnlyList<PlastLevel> ThroughSenior =
    [
        .. ThroughYouth,
        PlastLevel.Starshoplastun
    ];

    private static readonly IReadOnlyList<PlastLevel> SeniorLevels =
    [
        PlastLevel.Senior,
        PlastLevel.SeniorPratsi,
        PlastLevel.SeniorDovirja,
        PlastLevel.SeniorKerivnytstva
    ];

    /// <summary>The branch a ступінь belongs to: старший пластун is УСП, a сеніор's ступені are УПС, the rest УПЮ.</summary>
    public static KurinBranch BranchOf(PlastLevel level) => level switch
    {
        PlastLevel.Starshoplastun => KurinBranch.USP,
        _ when SeniorLevels.Contains(level) => KurinBranch.UPS,
        _ => KurinBranch.UPYu
    };

    /// <summary>The highest ступінь by its place on the ladder, not by its number; null when there is none.</summary>
    public static PlastLevel? Highest(IEnumerable<PlastLevel> levels)
    {
        var all = levels.ToList();
        return all.Count == 0 ? null : all.MaxBy(Rank);
    }

    private static int Rank(PlastLevel level)
    {
        for (var i = 0; i < InOrder.Count; i++)
        {
            if (InOrder[i] == level)
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>
    /// The branch of the person themselves where they are looked at. A senior ступінь outweighs the
    /// kurin: a впорядник in a УПЮ kurin is a старший пластун or a сеніор, and the youth programme is
    /// not theirs. Without one, the kurin's branch decides. The same rule the card draws on screen.
    /// </summary>
    public static KurinBranch PersonalBranch(IEnumerable<PlastLevel> levels, KurinBranch? membershipBranch)
    {
        var levelBranch = Highest(levels) is { } top ? BranchOf(top) : KurinBranch.UPYu;
        return levelBranch != KurinBranch.UPYu ? levelBranch : membershipBranch ?? KurinBranch.UPYu;
    }

    /// <summary>
    /// What a kurin of this branch is shown by default. Cumulative on purpose: a УПС kurin still
    /// wants to see when its people were заприсяжені. This is a **default**, not a restriction —
    /// the провід turns individual columns on and off from there.
    /// </summary>
    public static IReadOnlyList<PlastLevel> DefaultFor(KurinBranch branch)
        => branch switch
        {
            KurinBranch.USP => ThroughSenior,
            KurinBranch.UPS => InOrder,
            _ => ThroughYouth
        };
}
