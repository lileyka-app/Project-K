using ProjectK.Common.Models.Enums;

namespace ProjectK.Common.Models.Tags;

/// <summary>The rules of kurin tags that the server and the client share.</summary>
public static class KurinTagRules
{
    /// <summary>A person carries at most this many tags in one kurin.</summary>
    public const int MaxTagsPerMember = 10;

    public const int MaxNameLength = 40;

    /// <summary>
    /// The colours a tag may take: the closed palette of event groups (<c>BRANDBOOK.md</c> §2),
    /// mirrored by the client. A colour outside it is refused.
    /// </summary>
    public static readonly IReadOnlyList<string> Palette =
        ["#2F855A", "#2B6CB0", "#B7791F", "#9B2C2C", "#6B46C1", "#0E7490", "#4A5568"];

    /// <summary>
    /// Whether a kurin that has not said otherwise uses tags. A senior kurin tells its people apart by
    /// tags, so they are on; in a УПЮ kurin гуртки do that and tags are off until the kurin turns them on.
    /// The one place this default is decided.
    /// </summary>
    public static bool EnabledByDefault(KurinBranch branch) => branch != KurinBranch.UPYu;

    /// <summary>Whether <paramref name="colorHex"/> is one of the palette's colours.</summary>
    public static bool IsPaletteColor(string? colorHex) =>
        colorHex is not null && Palette.Contains(colorHex.Trim(), StringComparer.OrdinalIgnoreCase);
}
