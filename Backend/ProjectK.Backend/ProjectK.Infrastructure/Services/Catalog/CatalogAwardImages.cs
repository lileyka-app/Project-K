using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using ProjectK.Common.Contracts.ProbesAndBadgesModule;
using ProjectK.Common.Models.Catalog;

namespace ProjectK.Infrastructure.Services.Catalog;

/// <summary>
/// Відзначення pictures, fetched from the catalogue once and kept. Their URLs carry a content hash,
/// so a URL never changes what it points at and the cache needs no expiry.
/// </summary>
internal sealed class CatalogAwardImages : IAwardImagesCatalog
{
    private readonly CatalogStore _store;
    private readonly IHttpClientFactory _httpClients;
    private readonly ILogger<CatalogAwardImages> _logger;
    private readonly ConcurrentDictionary<string, CatalogImage> _cache = new();

    public CatalogAwardImages(CatalogStore store, IHttpClientFactory httpClients, ILogger<CatalogAwardImages> logger)
    {
        _store = store;
        _httpClients = httpClients;
        _logger = logger;
    }

    public async Task<CatalogImage?> GetAwardImageAsync(int level, bool colored, CancellationToken cancellationToken = default)
    {
        if (!_store.State.AwardImages.TryGetValue(level, out var images))
        {
            return null;
        }

        var url = colored ? images.Color : images.Bw;
        if (string.IsNullOrEmpty(url))
        {
            return null;
        }

        if (_cache.TryGetValue(url, out var cached))
        {
            return cached;
        }

        try
        {
            using var response = await _httpClients.CreateClient(CatalogClient.HttpClientName).GetAsync(url, cancellationToken);
            response.EnsureSuccessStatusCode();
            var image = new CatalogImage(
                await response.Content.ReadAsByteArrayAsync(cancellationToken),
                response.Content.Headers.ContentType?.MediaType ?? "image/png");
            return _cache.GetOrAdd(url, image);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            _logger.LogWarning(ex, "Could not fetch the award image {Url}.", url);
            return null;
        }
    }
}
