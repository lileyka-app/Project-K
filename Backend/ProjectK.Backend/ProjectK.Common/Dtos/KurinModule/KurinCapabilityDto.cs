using ProjectK.Common.Enums.AuthModule;

namespace ProjectK.Common.Dtos.KurinModule;

/// <summary>One row of the policy.</summary>
/// <param name="Key">The capability.</param>
/// <param name="Group">The section it sits in.</param>
/// <param name="YouthConfigurable">Whether the Youth column may be changed on this row.</param>
/// <param name="Reaches">The reaches a cell of this row may hold, narrowest first.</param>
/// <param name="Cells">One cell per audience, in column order.</param>
public sealed record KurinCapabilityDto(
    KurinCapability Key,
    CapabilityGroup Group,
    bool YouthConfigurable,
    IReadOnlyList<CapabilityReach> Reaches,
    IReadOnlyList<AccessPolicyCellDto> Cells);
