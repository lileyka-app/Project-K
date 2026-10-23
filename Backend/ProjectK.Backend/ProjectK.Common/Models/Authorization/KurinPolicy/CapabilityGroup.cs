using System.Text.Json.Serialization;

namespace ProjectK.Common.Models.Authorization.KurinPolicy;

/// <summary>The section of the settings page a capability sits in.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<CapabilityGroup>))]
public enum CapabilityGroup
{
    /// <summary>Дані юнаків: what of a person's card others see.</summary>
    MemberData,

    /// <summary>Перестороги й відзначення.</summary>
    WarningsAwards,

    /// <summary>Проби й вмілості.</summary>
    ProbesBadges,

    /// <summary>Журнал дій.</summary>
    Journal
}
