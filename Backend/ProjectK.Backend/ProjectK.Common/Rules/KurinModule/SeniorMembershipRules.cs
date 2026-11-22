using ProjectK.Common.Enums.KurinModule;

namespace ProjectK.Common.Rules.KurinModule;

/// <summary>
/// A person stands in one УСП or УПС kurin at most, as in Пласт. Alongside it they may be a впорядник
/// of an УПЮ kurin, or still a юнак there while moving up; another senior kurin takes them only by
/// «перевести».
/// </summary>
public static class SeniorMembershipRules
{
    /// <summary>Whether joining a kurin of <paramref name="toBranch"/> would put the person in a second senior kurin.</summary>
    /// <param name="otherOpenBranches">The main гілки of the kurins they stand in now, the joined one left out.</param>
    public static bool WouldStandInTwo(KurinBranch toBranch, IEnumerable<KurinBranch> otherOpenBranches)
        => KurinBranchSetRules.IsSenior(toBranch) && otherOpenBranches.Any(KurinBranchSetRules.IsSenior);
}
