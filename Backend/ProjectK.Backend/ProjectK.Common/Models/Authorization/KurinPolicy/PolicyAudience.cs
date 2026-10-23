using System.Text.Json.Serialization;

namespace ProjectK.Common.Models.Authorization.KurinPolicy;

/// <summary>
/// A column of the kurin's policy: the people a capability is given to. Offices fold into these
/// in <see cref="PolicyAudiences.For"/> only. Declared in the order the settings page shows them.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<PolicyAudience>))]
public enum PolicyAudience
{
    /// <summary>The Звʼязковий. Locked to the whole kurin on everything.</summary>
    Zvyazkovyi,

    /// <summary>Впорядники (КВ виховники).</summary>
    Vykhovnyk,

    /// <summary>Інструктори: the КВ without a гурток of their own.</summary>
    Instruktor,

    /// <summary>The Курінний.</summary>
    Kurinnyy,

    /// <summary>The rest of the курінний провід.</summary>
    KurinProvid,

    /// <summary>A Гуртковий.</summary>
    Hurtkovyy,

    /// <summary>The rest of a гуртковий провід.</summary>
    GroupProvid,

    /// <summary>Every member: the baseline each account carries.</summary>
    Youth
}
