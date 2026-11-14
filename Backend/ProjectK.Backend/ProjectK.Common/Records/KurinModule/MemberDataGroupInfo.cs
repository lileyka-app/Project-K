using ProjectK.Common.Enums.AuthModule;
using ProjectK.Common.Enums.KurinModule;
using ProjectK.Common.Rules.KurinModule;

namespace ProjectK.Common.Records.KurinModule;

/// <summary>One row of <see cref="MemberDataFieldRules"/>.</summary>
/// <param name="Group">The group.</param>
/// <param name="Name">How the group is named to clients in <c>hiddenData</c>.</param>
/// <param name="Capability">The capability of the kurin's policy that shows it.</param>
/// <param name="Fields">The response fields it covers, by their JSON names.</param>
public sealed record MemberDataGroupInfo(
    MemberDataGroup Group,
    string Name,
    KurinCapability Capability,
    IReadOnlyList<string> Fields);
