namespace ProjectK.BusinessLogic.Modules.ProbesAndBadgesModule.Services;

/// <summary>
/// What the kurin's journal calls a проба, its point or a вмілість: the catalogue's title at the
/// time, or the id when the catalogue does not know it. A snapshot, so the entry still reads the
/// same after the catalogue changes.
/// </summary>
public static class ProgressAuditLabels
{
    /// <summary>The проба's title.</summary>
    public static string Probe(IProbesCatalogService probes, string probeId) =>
        probes.GetGroupedProbeById(probeId)?.Title is { Length: > 0 } title ? title : probeId;

    /// <summary>The point's title within its проба.</summary>
    public static string ProbePoint(IProbesCatalogService probes, string probeId, string pointId)
    {
        var point = probes.GetGroupedProbeById(probeId)?.Sections
            .SelectMany(section => section.Points)
            .FirstOrDefault(candidate => string.Equals(candidate.Id, pointId, StringComparison.OrdinalIgnoreCase));

        return point?.Title is { Length: > 0 } title ? title : pointId;
    }

    /// <summary>The вмілість's title.</summary>
    public static string Badge(IBadgesCatalogService badges, string badgeId) =>
        badges.GetBadgeById(badgeId)?.Title is { Length: > 0 } title ? title : badgeId;
}
