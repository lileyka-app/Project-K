using ProjectK.Common.Dtos.ProbesAndBadgesModule;

namespace ProjectK.Common.Contracts.ProbesAndBadgesModule;

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
