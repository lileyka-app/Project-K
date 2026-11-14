using System.Text.Json.Serialization;

namespace ProjectK.Common.Enums.KurinModule;

/// <summary>Whose перестороги someone may cancel.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<WarningCancelAbility>))]
public enum WarningCancelAbility
{
    /// <summary>None at all.</summary>
    None,

    /// <summary>Only those they gave themselves, of a level they may still give.</summary>
    OwnIssued,

    /// <summary>Anyone's.</summary>
    Any
}
