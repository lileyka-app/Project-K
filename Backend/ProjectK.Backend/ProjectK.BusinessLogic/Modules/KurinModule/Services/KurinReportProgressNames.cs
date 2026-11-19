using ProjectK.Common.Dtos.ProbesAndBadgesModule;

namespace ProjectK.BusinessLogic.Modules.KurinModule.Services;

/// <summary>How the звіт куреня names a проба, from the catalogue's entry.</summary>
public static class KurinReportProgressNames
{
    /// <summary>The проба's title, or its id when the catalogue has none.</summary>
    public static string ProbeTitle(string probeId, GroupedProbeDto? probe)
        => string.IsNullOrWhiteSpace(probe?.Title) ? probeId : probe.Title;
}
