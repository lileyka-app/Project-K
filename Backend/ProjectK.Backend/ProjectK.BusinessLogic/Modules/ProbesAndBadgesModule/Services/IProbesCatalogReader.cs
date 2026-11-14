using ProjectK.Common.Dtos.ProbesAndBadgesModule;

namespace ProjectK.BusinessLogic.Modules.ProbesAndBadgesModule.Services;

public interface IProbesCatalogReader
{
    IReadOnlyList<ProbeSummaryDto> GetProbes();

    GroupedProbeDto? GetGroupedProbeById(string probeId);
}
