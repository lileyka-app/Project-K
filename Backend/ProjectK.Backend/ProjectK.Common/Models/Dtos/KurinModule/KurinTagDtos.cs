namespace ProjectK.Common.Models.Dtos.KurinModule;

/// <summary>A tag of the kurin.</summary>
/// <param name="KurinTagKey"></param>
/// <param name="Name"></param>
/// <param name="ColorHex"></param>
/// <param name="MemberCount">How many people carry it; null for whoever may not manage tags.</param>
public sealed record KurinTagDto(Guid KurinTagKey, string Name, string ColorHex, int? MemberCount);

/// <summary>
/// The kurin's tags as the viewer may use them. With tags off the list is empty, and the client
/// shows tags nowhere.
/// </summary>
/// <param name="Enabled">Whether the kurin uses tags.</param>
/// <param name="CanManage">Whether the viewer may define tags and put them on people.</param>
/// <param name="MaxPerMember">How many tags one person may carry.</param>
/// <param name="Palette">The colours a tag may take.</param>
/// <param name="Tags"></param>
public sealed record KurinTagsDto(
    bool Enabled,
    bool CanManage,
    int MaxPerMember,
    IReadOnlyList<string> Palette,
    IReadOnlyList<KurinTagDto> Tags);

/// <summary>The kurin's tag switches, as the settings page shows them.</summary>
/// <param name="Enabled">Whether the kurin uses tags now.</param>
/// <param name="EnabledByDefault">What the kurin would have if it had never chosen: on in a senior kurin.</param>
/// <param name="OwnTagsVisible">Whether a person sees the tags on themselves.</param>
public sealed record KurinTagSettingsDto(bool Enabled, bool EnabledByDefault, bool OwnTagsVisible);
