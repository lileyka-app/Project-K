namespace ProjectK.Common.Dtos.KurinModule;

/// <summary>The kurin's tag switches, as the settings page shows them.</summary>
/// <param name="Enabled">Whether the kurin uses tags now.</param>
/// <param name="EnabledByDefault">What the kurin would have if it had never chosen: on in a senior kurin.</param>
/// <param name="OwnTagsVisible">Whether a person sees the tags on themselves.</param>
public sealed record KurinTagSettingsDto(bool Enabled, bool EnabledByDefault, bool OwnTagsVisible);
