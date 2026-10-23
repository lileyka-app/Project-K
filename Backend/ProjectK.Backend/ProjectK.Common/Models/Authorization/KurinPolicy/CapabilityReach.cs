using System.Text.Json.Serialization;

namespace ProjectK.Common.Models.Authorization.KurinPolicy;

/// <summary>How far a capability reaches for one audience. Wider values compare greater.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<CapabilityReach>))]
public enum CapabilityReach
{
    /// <summary>Nobody.</summary>
    None,

    /// <summary>Only the гуртки the person leads.</summary>
    OwnGroup,

    /// <summary>The whole kurin.</summary>
    Kurin
}

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
