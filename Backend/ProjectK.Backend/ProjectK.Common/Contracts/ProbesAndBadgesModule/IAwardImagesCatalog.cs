using ProjectK.Common.Records.ProbesAndBadgesModule;

namespace ProjectK.Common.Contracts.ProbesAndBadgesModule;

/// <summary>Pictures of the УПЮ відзначення, by level.</summary>
public interface IAwardImagesCatalog
{
    Task<CatalogImage?> GetAwardImageAsync(int level, bool colored, CancellationToken cancellationToken = default);
}
