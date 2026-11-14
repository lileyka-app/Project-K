using ProjectK.Common.Enums.AuthModule;

namespace ProjectK.Common.Rules.AuthModule;

/// <summary>Conversions between a reach and the permission scope it grants.</summary>
public static class CapabilityReachExtensions
{
    /// <summary>The scope a reach grants; null for <see cref="CapabilityReach.None"/>.</summary>
    public static AccessScope? ToScope(this CapabilityReach reach) => reach switch
    {
        CapabilityReach.Kurin => AccessScope.KurinWide,
        CapabilityReach.OwnGroup => AccessScope.OwnGroups,
        _ => null
    };
}
