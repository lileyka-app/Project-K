using ProjectK.Common.Enums.KurinModule;
using ProjectK.Common.Enums.Shared;

namespace ProjectK.Common.Rules.Shared;

/// <summary>
/// A person's улад — УПЮ, УСП or УПС — worked out, never stored. Their confirmed ступені decide, and a
/// senior one outweighs the kurin (<see cref="PlastLadderRules.PersonalBranch"/>); without one, the
/// гілка of the membership they are looked at in. The ступені are the member module's, the гілка the
/// kurin's, so the rule lives between them. A ступінь that awaits the Звʼязковий is not passed in:
/// it does not count. Clients render youth or senior by this answer and do not work it out again.
/// </summary>
public static class UladRules
{
    /// <summary>
    /// The улад where the person stands in a kurin of <paramref name="membershipBranch"/>, from the
    /// ступені that count; УПЮ when they have none and stand nowhere.
    /// </summary>
    public static KurinBranch Of(IEnumerable<PlastLevel> countingLevels, KurinBranch? membershipBranch) =>
        PlastLadderRules.PersonalBranch(countingLevels, membershipBranch);

    /// <summary>
    /// The улад across every kurin the person stands in. A current membership in an УПЮ kurin keeps the
    /// youth programme theirs; otherwise the first гілка given decides.
    /// </summary>
    public static KurinBranch Of(IEnumerable<PlastLevel> countingLevels, IEnumerable<KurinBranch> standingBranches)
    {
        var standing = standingBranches.ToList();
        if (standing.Contains(KurinBranch.UPYu))
        {
            return Of(countingLevels, KurinBranch.UPYu);
        }

        return Of(countingLevels, standing.Count > 0 ? standing[0] : null);
    }
}
