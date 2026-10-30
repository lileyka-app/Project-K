using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using ProjectK.Common.Interfaces.Modules.ProbesAndBadgesModule;
using ProjectK.Common.Models.Catalog;

namespace ProjectK.BusinessLogic.Modules.ProbesAndBadgesModule.Services;

public sealed class BadgesCatalogService : IBadgesCatalogService
{
    // Every key carries the catalogue's version: the catalogue changes under this cache (a new
    // fetch, a renamed край), and a six-hour entry would keep serving the old one.
    private static readonly TimeSpan CatalogCacheTtl = TimeSpan.FromHours(6);

    private readonly IBadgesCatalog _badgesCatalog;
    private readonly IMemoryCache _cache;
    private readonly ILogger<BadgesCatalogService> _logger;

    public BadgesCatalogService(
        IBadgesCatalog badgesCatalog,
        IMemoryCache cache,
        ILogger<BadgesCatalogService> logger)
    {
        _badgesCatalog = badgesCatalog;
        _cache = cache;
        _logger = logger;
    }

    public BadgesMetadata GetBadgesMetadata()
    {
        var cacheKey = $"catalog:badges:metadata:{_badgesCatalog.Version}";

        if (_cache.TryGetValue(cacheKey, out BadgesMetadata? cachedMetadata))
        {
            _logger.LogDebug("Badges cache hit. Key={CacheKey}", cacheKey);
            return cachedMetadata!;
        }

        _logger.LogDebug("Badges cache miss. Key={CacheKey}", cacheKey);

        var metadata = _badgesCatalog.Metadata;
        _cache.Set(cacheKey, metadata, CatalogCacheTtl);

        _logger.LogDebug(
            "Badges cache set. Key={CacheKey}, TtlMinutes={TtlMinutes}",
            cacheKey,
            CatalogCacheTtl.TotalMinutes);

        return metadata;
    }

    public IReadOnlyList<Badge> GetBadges(int take)
    {
        var safeTake = Math.Clamp(take, 1, 1000);
        var cacheKey = $"catalog:badges:list:{_badgesCatalog.Version}:{safeTake}";

        if (_cache.TryGetValue(cacheKey, out IReadOnlyList<Badge>? cachedBadges))
        {
            _logger.LogDebug(
                "Badges cache hit. Key={CacheKey}, Take={Take}",
                cacheKey,
                safeTake);
            return cachedBadges!;
        }

        _logger.LogDebug(
            "Badges cache miss. Key={CacheKey}, Take={Take}",
            cacheKey,
            safeTake);

        var badges = _badgesCatalog.GetAll().Take(safeTake).ToList().AsReadOnly();
        _cache.Set(cacheKey, badges, CatalogCacheTtl);

        _logger.LogDebug(
            "Badges cache set. Key={CacheKey}, Take={Take}, TtlMinutes={TtlMinutes}",
            cacheKey,
            safeTake,
            CatalogCacheTtl.TotalMinutes);

        return badges;
    }

    public Badge? GetBadgeById(string id)
    {
        var cacheKey = $"catalog:badges:by-id:{_badgesCatalog.Version}:{id}";

        if (_cache.TryGetValue(cacheKey, out Badge? cachedBadge))
        {
            _logger.LogDebug("Badges cache hit. Key={CacheKey}, Id={BadgeId}", cacheKey, id);
            return cachedBadge;
        }

        _logger.LogDebug("Badges cache miss. Key={CacheKey}, Id={BadgeId}", cacheKey, id);

        var badge = _badgesCatalog.GetById(id);
        if (badge is not null)
        {
            _cache.Set(cacheKey, badge, CatalogCacheTtl);
            _logger.LogDebug(
                "Badges cache set. Key={CacheKey}, Id={BadgeId}, TtlMinutes={TtlMinutes}",
                cacheKey,
                id,
                CatalogCacheTtl.TotalMinutes);
        }
        else
        {
            _logger.LogDebug("Badges cache skip. Key={CacheKey}, Id={BadgeId}, Reason=NotFound", cacheKey, id);
        }

        return badge;
    }
}
