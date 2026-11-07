using System;
using System.Collections.Generic;
using System.Linq;

namespace ProjectK.Common.Models.Enums;

/// <summary>
/// Which гілки a kurin may hold together. A УПЮ kurin is УПЮ alone; a senior kurin is УСП, УПС or
/// both. Nothing mixes юнацтво with the older гілки: that would be two kurins, not one.
/// </summary>
public static class KurinBranchSet
{
    /// <summary>The order гілки are listed and shown in; the first one a kurin holds is its main гілка.</summary>
    private static readonly KurinBranch[] Order = [KurinBranch.UPYu, KurinBranch.USP, KurinBranch.UPS];

    public const string InvalidCode = "InvalidKurinBranches";
    public const string InvalidMessage = "A kurin is either УПЮ alone, or УСП, УПС or both, each named once.";

    public static bool IsSenior(KurinBranch branch) => branch is KurinBranch.USP or KurinBranch.UPS;

    public static bool IsValid(IReadOnlyCollection<KurinBranch> branches)
    {
        if (branches.Count == 0 || branches.Distinct().Count() != branches.Count)
        {
            return false;
        }

        return branches.All(IsSenior) || (branches.Count == 1 && branches.Single() == KurinBranch.UPYu);
    }

    /// <summary>The items in the order their гілки are shown.</summary>
    public static IEnumerable<T> Ordered<T>(IEnumerable<T> items, Func<T, KurinBranch> branchOf)
        => items.OrderBy(item => Array.IndexOf(Order, branchOf(item)));
}
