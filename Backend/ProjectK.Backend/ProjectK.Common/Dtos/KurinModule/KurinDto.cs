using ProjectK.Common.Enums.KurinModule;
using ProjectK.Common.Records.KurinModule;

namespace ProjectK.Common.Dtos.KurinModule;

/// <summary>A kurin as its pages and the administrators' list see it.</summary>
/// <param name="KurinKey">The kurin.</param>
/// <param name="Number">The kurin's number; null for a підготовчий курінь.</param>
/// <param name="Branch">Яка гілка. Read by the UI, which shows юнацькі речі only where they mean something.</param>
/// <param name="Branches">Every гілка with the kurin's number there, main one first: «5-й курінь УСП · 20-й курінь УПС».</param>
/// <param name="KraiCode">The край it works under.</param>
/// <param name="Stanytsia">Its станиця, as the kurin wrote it.</param>
/// <param name="RegionOrCountry">Its region or country, as the kurin wrote it.</param>
/// <param name="NamedAfter">Whom the kurin is named after.</param>
/// <param name="Description">The kurin's own words about itself.</param>
/// <param name="CurrentUserCount">How many active accounts stand in it now.</param>
/// <param name="ProfileVerificationEnabled">Whether its people's profiles are checked by the провід.</param>
/// <param name="ScoreEnabled">Whether the kurin keeps точкування now; the UI shows its menu, tiles and pages only then.</param>
/// <param name="RoleNames">The kurin's own names for its roles, by role: «Pysar» → «Скриптор». Empty keeps the usual names.</param>
/// <param name="EmblemUrl">A senior kurin's emblem, as a link that expires; null when it has none or is УПЮ.</param>
/// <param name="CanRenameRoles">Whether the kurin may rename its roles at all: senior kurins for now.</param>
public sealed record KurinDto(
    Guid KurinKey,
    int? Number,
    KurinBranch Branch,
    IReadOnlyList<KurinNumbering> Branches,
    string KraiCode,
    string? Stanytsia,
    string? RegionOrCountry,
    string? NamedAfter,
    string? Description,
    int CurrentUserCount,
    bool ProfileVerificationEnabled,
    bool ScoreEnabled,
    IReadOnlyDictionary<string, string> RoleNames,
    string? EmblemUrl,
    bool CanRenameRoles)
{
    /// <summary>A підготовчий курінь has no number yet.</summary>
    public bool IsPreparatory => Number is null;
}
