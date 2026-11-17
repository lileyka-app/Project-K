using ProjectK.Common.Dtos.ProbesAndBadgesModule;

namespace ProjectK.BusinessLogic.Modules.KurinModule.Services;

/// <summary>How the звіт куреня names a проба and one of its points, from the catalogue's entry.</summary>
public static class KurinReportProgressNames
{
    /// <summary>The проба's title, or its id when the catalogue has none.</summary>
    public static string ProbeTitle(string probeId, GroupedProbeDto? probe)
        => string.IsNullOrWhiteSpace(probe?.Title) ? probeId : probe.Title;

    /// <summary>
    /// «точка Б3 - title»: the section's code and the point's place in it, or the point's id when the
    /// catalogue does not know the point.
    /// </summary>
    public static string ProbePointLabel(string pointId, GroupedProbeDto? probe)
    {
        if (probe?.Sections is null)
        {
            return pointId;
        }

        foreach (var section in probe.Sections)
        {
            var points = section.Points ?? [];
            var pointIndex = points
                .Select((point, index) => new { point, index })
                .FirstOrDefault(item => item.point.Id == pointId)
                ?.index ?? -1;
            if (pointIndex < 0)
            {
                continue;
            }

            var point = points[pointIndex];
            var pointCode = !string.IsNullOrWhiteSpace(section.Code)
                ? $"{section.Code}{pointIndex + 1}"
                : pointId;
            return string.IsNullOrWhiteSpace(point.Title)
                ? $"точка {pointCode}"
                : $"точка {pointCode} - {point.Title}";
        }

        return pointId;
    }
}
