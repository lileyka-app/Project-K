using ProjectK.Common.Enums.IdentityModule;

namespace ProjectK.Common.Dtos.KurinModule;

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
