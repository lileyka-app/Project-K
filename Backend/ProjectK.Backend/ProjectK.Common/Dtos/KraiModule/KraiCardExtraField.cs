using ProjectK.Common.Rules.KraiModule;

namespace ProjectK.Common.Dtos.KraiModule;

/// <summary>A field a край adds to the card. Its value is kept on the member under <see cref="Key"/>.</summary>
public sealed record KraiCardExtraField
{
    public string Key { get; init; } = string.Empty;
    public string Label { get; init; } = string.Empty;

    /// <summary><see cref="KraiCardFields.Text"/> or <see cref="KraiCardFields.Date"/>.</summary>
    public string Type { get; init; } = KraiCardFields.Text;

    public bool Required { get; init; }
}
