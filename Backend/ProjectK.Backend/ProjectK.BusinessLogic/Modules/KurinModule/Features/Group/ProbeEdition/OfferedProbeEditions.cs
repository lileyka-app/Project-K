using ProjectK.Common.Interfaces.Modules.ProbesAndBadgesModule;

namespace ProjectK.BusinessLogic.Modules.KurinModule.Features.Group.ProbeEdition;

/// <summary>
/// The editions a kurin's гуртки may choose: the unnamed one always, and a named one only when it
/// is the kurin's own край's — the 2026 УПЮ programme is for the край Україна alone.
/// </summary>
internal static class OfferedProbeEditions
{
    public static IReadOnlyList<ProbeEditionDto> OfferedTo(IProbesCatalog catalog, string kraiCode)
    {
        return catalog.GetAll()
            .GroupBy(probe => probe.Edition)
            .Where(edition => edition.Key is null
                || edition.All(probe => string.Equals(probe.KraiCode, kraiCode, StringComparison.OrdinalIgnoreCase)))
            .OrderBy(edition => edition.Key is not null)
            .ThenBy(edition => edition.Key, StringComparer.Ordinal)
            .Select(edition => new ProbeEditionDto(
                edition.Key,
                edition.Select(probe => probe.EditionTitle).FirstOrDefault(title => title is not null),
                edition.Any(probe => probe.StatusCode == "trial"),
                [.. edition.OrderBy(probe => probe.Order).Select(probe => probe.Title)]))
            .ToList();
    }

    /// <summary>
    /// The edition a гурток actually walks: the one it chose while the catalogue still offers it,
    /// otherwise the unnamed one — the same fallback the probe line makes for its youths.
    /// </summary>
    public static string? Effective(IReadOnlyList<ProbeEditionDto> offered, string? chosen) =>
        offered.Any(edition => edition.Id == chosen) ? chosen : null;
}
