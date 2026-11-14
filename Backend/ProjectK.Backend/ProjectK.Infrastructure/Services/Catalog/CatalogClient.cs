using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Extensions.Options;
using ProjectK.Common.Options.ProbesAndBadgesModule;

namespace ProjectK.Infrastructure.Services.Catalog;

/// <summary>Reads <c>GET /v1/catalog</c> from the catalogue service.</summary>
internal sealed class CatalogClient
{
    public const string HttpClientName = "catalog";
    public const int SupportedSchemaVersion = 2;

    private readonly IHttpClientFactory _httpClients;
    private readonly CatalogOptions _options;

    public CatalogClient(IHttpClientFactory httpClients, IOptions<CatalogOptions> options)
    {
        _httpClients = httpClients;
        _options = options.Value;
    }

    public string CatalogUrl
    {
        get
        {
            var url = $"{_options.BaseUrl.TrimEnd('/')}/v1/catalog";
            return string.IsNullOrWhiteSpace(_options.Krai) ? url : $"{url}?krai={Uri.EscapeDataString(_options.Krai.Trim())}";
        }
    }

    /// <summary>The catalogue's raw bytes and ETag, or null when it has not changed since <paramref name="etag"/>.</summary>
    public async Task<(byte[] Body, string? ETag)?> FetchAsync(string? etag, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, CatalogUrl);
        if (!string.IsNullOrEmpty(etag) && EntityTagHeaderValue.TryParse(etag, out var tag))
        {
            request.Headers.IfNoneMatch.Add(tag);
        }

        using var response = await _httpClients.CreateClient(HttpClientName).SendAsync(request, cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotModified)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadAsByteArrayAsync(cancellationToken);
        return (body, response.Headers.ETag?.ToString());
    }

    public static CatalogDocument Parse(byte[] body)
    {
        var document = JsonSerializer.Deserialize<CatalogDocument>(body)
            ?? throw new InvalidDataException("The catalogue answered with an empty document.");
        if (document.SchemaVersion != SupportedSchemaVersion)
        {
            throw new InvalidDataException(
                $"The catalogue speaks schema {document.SchemaVersion}; this build reads {SupportedSchemaVersion}.");
        }

        return document;
    }
}
