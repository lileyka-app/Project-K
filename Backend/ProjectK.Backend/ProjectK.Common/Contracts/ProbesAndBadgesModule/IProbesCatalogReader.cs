using ProjectK.Common.Dtos.ProbesAndBadgesModule;

namespace ProjectK.Common.Contracts.ProbesAndBadgesModule;

public interface IProbesCatalogReader
{
    IReadOnlyList<ProbeSummaryDto> GetProbes();

    GroupedProbeDto? GetGroupedProbeById(string probeId);
}
