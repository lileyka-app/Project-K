using ProjectK.Common.Models.Catalog;

namespace ProjectK.Common.Contracts.ProbesAndBadgesModule;

/// <summary>Pictures of the УПЮ відзначення, by level.</summary>
public interface IAwardImagesCatalog
{
    Task<CatalogImage?> GetAwardImageAsync(int level, bool colored, CancellationToken cancellationToken = default);
}
