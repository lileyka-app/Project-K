using ProjectK.Common.Models.Dtos.ProbesAndBadgesModule;

namespace ProjectK.BusinessLogic.Modules.ProbesAndBadgesModule.Services;

public interface IProbesCatalogService
{
    IReadOnlyList<ProbeSummaryDto> GetProbes();

    GroupedProbeDto? GetGroupedProbeById(string probeId);
}
