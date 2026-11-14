using ProjectK.Common.Rules.KraiModule;

namespace ProjectK.Common.Dtos.KraiModule;

public sealed record KraiCardFieldsDocument
{
    /// <summary>
    /// How the card treats each of its own fields that a край may change, by name
    /// (<see cref="KraiCardFields.Configurable"/>): one of <see cref="KraiCardFields.Required"/>,
    /// <see cref="KraiCardFields.Optional"/> or <see cref="KraiCardFields.Hidden"/>.
    /// </summary>
    public IReadOnlyDictionary<string, string>? Fields { get; init; }

    /// <summary>Fields of the край's own that the card does not have, such as a visa type.</summary>
    public IReadOnlyList<KraiCardExtraField>? Extra { get; init; }
}
