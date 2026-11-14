using ProjectK.Common.Models.Catalog;

namespace ProjectK.Common.Contracts.ProbesAndBadgesModule;

/// <summary>The probes from the catalogue service, as last fetched.</summary>
public interface IProbesCatalog
{
    /// <summary>The same counter as <see cref="IBadgesCatalog.Version"/>.</summary>
    long Version { get; }

    IReadOnlyList<Probe> GetAll();

    Probe? GetById(string id);
}
