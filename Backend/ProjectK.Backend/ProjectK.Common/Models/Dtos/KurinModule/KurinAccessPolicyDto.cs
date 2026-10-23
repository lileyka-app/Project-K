using ProjectK.Common.Models.Authorization.KurinPolicy;

namespace ProjectK.Common.Models.Dtos.KurinModule;

/// <summary>A kurin's «Права й видимість» as its settings page shows it.</summary>
/// <param name="Audiences">The columns, in order.</param>
/// <param name="Capabilities">The rows, in order.</param>
public sealed record KurinAccessPolicyDto(
    IReadOnlyList<PolicyAudience> Audiences,
    IReadOnlyList<KurinCapabilityDto> Capabilities);

/// <summary>One row of the policy.</summary>
/// <param name="Key">The capability.</param>
/// <param name="Group">The section it sits in.</param>
/// <param name="YouthConfigurable">Whether the Youth column may be changed on this row.</param>
/// <param name="Cells">One cell per audience, in column order.</param>
public sealed record KurinCapabilityDto(
    KurinCapability Key,
    CapabilityGroup Group,
    bool YouthConfigurable,
    IReadOnlyList<AccessPolicyCellDto> Cells);

/// <summary>What one audience gets for one capability.</summary>
/// <param name="Audience">The column.</param>
/// <param name="Effective">What the kurin has now.</param>
/// <param name="Default">What it would have without its own setting.</param>
/// <param name="Locked">Whether no kurin may change the cell.</param>
public sealed record AccessPolicyCellDto(
    PolicyAudience Audience,
    CapabilityReach Effective,
    CapabilityReach Default,
    bool Locked);
