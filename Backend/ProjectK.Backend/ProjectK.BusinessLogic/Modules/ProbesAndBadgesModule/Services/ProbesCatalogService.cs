using Microsoft.Extensions.Caching.Memory;
using ProjectK.BusinessLogic.Modules.ProbesAndBadgesModule.Models;
using ProjectK.Common.Interfaces.Modules.ProbesAndBadgesModule;

namespace ProjectK.BusinessLogic.Modules.ProbesAndBadgesModule.Services;

public sealed class ProbesCatalogService : IProbesCatalogService
{
    private static readonly TimeSpan CatalogCacheTtl = TimeSpan.FromHours(6);

    private readonly IProbesCatalog _probesCatalog;
    private readonly IMemoryCache _cache;

    public ProbesCatalogService(IProbesCatalog probesCatalog, IMemoryCache cache)
    {
        _probesCatalog = probesCatalog;
        _cache = cache;
    }

    public IReadOnlyList<ProbeSummaryResponse> GetProbes()
    {
        // Keyed by the catalogue's version, so a new edition shows up with the fetch that brought it.
        return _cache.GetOrCreate($"catalog:probes:list:{_probesCatalog.Version}", entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = CatalogCacheTtl;
            return _probesCatalog
                .GetAll()
                .Select(ProbeSummaryResponse.FromProbe)
                .ToList();
        })!;
    }

    public GroupedProbeResponse? GetGroupedProbeById(string probeId)
    {
        var cacheKey = $"catalog:probes:grouped:{_probesCatalog.Version}:{probeId}";

        if (_cache.TryGetValue(cacheKey, out GroupedProbeResponse? cachedProbe))
        {
            return cachedProbe;
        }

        var probe = _probesCatalog.GetById(probeId);
        if (probe is null)
        {
            return null;
        }

        var response = GroupedProbeResponse.FromProbe(probe);
        _cache.Set(cacheKey, response, CatalogCacheTtl);

        return response;
    }
}
