using ProjectK.Common.Models.Catalog;

namespace ProjectK.BusinessLogic.Modules.ProbesAndBadgesModule.Models;

/// <param name="Edition">The edition of the programme; null for the one every kurin has walked
/// since before editions were named.</param>
/// <param name="Order">The probe's place in its edition.</param>
/// <param name="StatusCode">"approved" or "trial".</param>
public sealed record ProbeSummaryResponse(
    string Id,
    string Title,
    int PointsCount,
    int SectionsCount,
    string? Edition = null,
    int Order = 0,
    string KraiCode = "",
    string StatusCode = ""
)
{
    public static ProbeSummaryResponse FromProbe(Probe probe)
    {
        return new ProbeSummaryResponse(
            probe.Id,
            probe.Title,
            probe.Points.Count,
            probe.Sections?.Count ?? 0,
            probe.Edition,
            probe.Order,
            probe.KraiCode,
            probe.StatusCode
        );
    }
}
