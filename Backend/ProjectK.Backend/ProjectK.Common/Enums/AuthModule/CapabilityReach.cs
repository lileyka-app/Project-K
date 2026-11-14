using System.Text.Json.Serialization;

namespace ProjectK.Common.Enums.AuthModule;

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
