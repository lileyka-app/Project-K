using ProjectK.Common.Dtos.ProbesAndBadgesModule;
using ProjectK.Common.Models.Catalog;
using CatalogProbe = ProjectK.Common.Models.Catalog.Probe;

namespace ProjectK.BusinessLogic.Modules.ProbesAndBadgesModule.Features.Catalog;

internal static class CatalogMapping
{
    public static ProbeSummaryDto ToSummaryDto(this CatalogProbe probe) => new(
        Id: probe.Id,
        Title: probe.Title,
        PointsCount: probe.Points.Count,
        SectionsCount: probe.Sections?.Count ?? 0,
        Edition: probe.Edition,
        Order: probe.Order,
        KraiCode: probe.KraiCode,
        StatusCode: probe.StatusCode);

    public static GroupedProbeDto ToGroupedDto(this CatalogProbe probe) => new(
        Id: probe.Id,
        Title: probe.Title,
        PointsCount: probe.Points.Count,
        SectionsCount: probe.Sections?.Count ?? 0,
        Sections: probe.Sections ?? Array.Empty<ProbeSection>());
}
