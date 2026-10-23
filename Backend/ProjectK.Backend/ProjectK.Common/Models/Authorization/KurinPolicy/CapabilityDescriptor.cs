using ProjectK.Common.Models.Enums;

namespace ProjectK.Common.Models.Authorization.KurinPolicy;

/// <summary>
/// One row of the kurin's policy: the permission a capability grants, what each audience gets
/// when the kurin has not said otherwise, and whether the Youth column may be changed at all.
/// </summary>
/// <param name="Key">The capability.</param>
/// <param name="Group">The settings section it is shown in.</param>
/// <param name="Resource">The resource of the permission it grants.</param>
/// <param name="Action">The action of the permission it grants. Leveled capabilities share it.</param>
/// <param name="Defaults">The reach of every audience by default; every audience is present.</param>
/// <param name="YouthConfigurable">
/// Whether a kurin may change what youths get. Only data rows: an action is never handed to the
/// baseline every account carries.
/// </param>
/// <param name="Reaches">
/// The reaches a cell of this row may hold, narrowest first. A right with no гурток to bound it —
/// aiming at the whole kurin — offers only nobody or the kurin.
/// </param>
public sealed record CapabilityDescriptor(
    KurinCapability Key,
    CapabilityGroup Group,
    ResourceType Resource,
    ResourceAction Action,
    IReadOnlyDictionary<PolicyAudience, CapabilityReach> Defaults,
    bool YouthConfigurable,
    IReadOnlyList<CapabilityReach> Reaches)
{
    /// <summary>Whether a cell of this row may hold <paramref name="reach"/>.</summary>
    public bool Offers(CapabilityReach reach) => Reaches.Contains(reach);

    /// <summary>Whether the audience's cell is fixed and no kurin may change it.</summary>
    public bool IsLocked(PolicyAudience audience) =>
        audience == PolicyAudience.Zvyazkovyi
        || (audience == PolicyAudience.Youth && !YouthConfigurable);
}
