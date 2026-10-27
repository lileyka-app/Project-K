using ProjectK.Common.Models.Catalog;

namespace ProjectK.Common.Interfaces.Modules.ProbesAndBadgesModule;

/// <summary>The вмілості from the catalogue service, as last fetched.</summary>
public interface IBadgesCatalog
{
    /// <summary>
    /// Changes whenever what the catalogue returns does: a new fetch, or a край named anew. A cache
    /// keyed by it never serves an older catalogue.
    /// </summary>
    long Version { get; }

    BadgesMetadata Metadata { get; }

    IReadOnlyList<Badge> GetAll();

    Badge? GetById(string id);
}

/// <summary>The probes from the catalogue service, as last fetched.</summary>
public interface IProbesCatalog
{
    /// <summary>The same counter as <see cref="IBadgesCatalog.Version"/>.</summary>
    long Version { get; }

    IReadOnlyList<Probe> GetAll();

    Probe? GetById(string id);
}

/// <summary>Pictures of the УПЮ відзначення, by level.</summary>
public interface IAwardImagesCatalog
{
    Task<CatalogImage?> GetAwardImageAsync(int level, bool colored, CancellationToken cancellationToken = default);
}
