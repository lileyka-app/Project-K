using ProjectK.Common.Enums.KurinModule;
using ProjectK.Common.Enums.MemberModule;
using ProjectK.Common.Records.MemberModule;
using ProjectK.Common.Rules.KurinModule;

namespace ProjectK.Common.Rules.MemberModule;

/// <summary>
/// Who counts as a senior: a person who stands in a УСП or УПС kurin now and is no longer a юнак
/// anywhere. Their probes, вмілості and УПЮ відзначення are then a closed youth record, which access
/// guards harder and the award form treats as theirs.
/// </summary>
public static class SeniorStandingRules
{
    /// <summary>
    /// Whether the open memberships make the person a senior. While they are still a юнак in an УПЮ
    /// kurin, they are a юнак, whatever else they are too.
    /// </summary>
    public static bool StandsInSeniorKurin(IReadOnlyCollection<MembershipBranchRow> open)
        => open.Any(row => KurinBranchSetRules.IsSenior(row.Branch))
           && !open.Any(row => row.Branch == KurinBranch.UPYu && row.Kind == MembershipKind.Youth);
}
