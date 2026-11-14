using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Records;

namespace ProjectK.Common.Models.Dtos.KurinModule;

/// <summary>A kurin as its pages and the administrators' list see it.</summary>
/// <param name="Number">The kurin's number; null for a підготовчий курінь.</param>
/// <param name="Branch">Яка гілка. Read by the UI, which shows юнацькі речі only where they mean something.</param>
/// <param name="Branches">Every гілка with the kurin's number there, main one first: «5-й курінь УСП · 20-й курінь УПС».</param>
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
