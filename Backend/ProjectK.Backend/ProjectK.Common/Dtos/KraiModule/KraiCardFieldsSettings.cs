using ProjectK.Common.Rules.KraiModule;

namespace ProjectK.Common.Dtos.KraiModule;

/// <summary>Every configurable field with its mode, and the край's own fields.</summary>
public sealed record KraiCardFieldsSettings(
    IReadOnlyDictionary<string, string> Fields,
    IReadOnlyList<KraiCardExtraField> Extra)
{
    /// <summary>The card as it is without a profile.</summary>
    public static readonly KraiCardFieldsSettings Default = new(KraiCardFields.Configurable, []);

    public string ModeOf(string field)
    {
        return Fields.TryGetValue(field, out var mode) ? mode : KraiCardFields.Configurable.GetValueOrDefault(field, KraiCardFields.Required);
    }

    /// <summary>
    /// The fields a kurin may leave off its card, with their names: the край's optional ones and
    /// its own fields that are not required. What the край requires a kurin cannot drop.
    /// </summary>
    public IReadOnlyList<(string Key, string Label)> Hideable()
    {
        return Fields
            .Where(field => field.Value == KraiCardFields.Optional)
            .Select(field => (field.Key, KraiCardFields.Labels.GetValueOrDefault(field.Key, field.Key)))
            .Concat(Extra.Where(field => !field.Required).Select(field => (field.Key, field.Label)))
            .ToList();
    }

    /// <summary>The card in a kurin that hides <paramref name="hidden"/>; a key it may not hide is ignored.</summary>
    public KraiCardFieldsSettings HidingFor(IReadOnlyCollection<string> hidden)
    {
        if (hidden.Count == 0)
        {
            return this;
        }

        var hideable = Hideable().Select(field => field.Key).Where(hidden.Contains).ToHashSet(StringComparer.Ordinal);
        return new KraiCardFieldsSettings(
            Fields.ToDictionary(field => field.Key, field => hideable.Contains(field.Key) ? KraiCardFields.Hidden : field.Value),
            Extra.Where(field => !hideable.Contains(field.Key)).ToList());
    }
}
