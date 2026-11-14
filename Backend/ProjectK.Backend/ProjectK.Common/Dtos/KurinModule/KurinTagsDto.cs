namespace ProjectK.Common.Dtos.KurinModule;

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
