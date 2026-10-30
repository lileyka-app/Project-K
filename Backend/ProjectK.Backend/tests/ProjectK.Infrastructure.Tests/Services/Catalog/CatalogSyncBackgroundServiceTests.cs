using System.Net;
using System.Net.Http.Headers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using ProjectK.Common.Entities.KraiModule;
using ProjectK.Common.Interfaces.Modules.ProbesAndBadgesModule;
using ProjectK.Common.Models.Settings;
using ProjectK.Infrastructure.DbContexts;
using ProjectK.Infrastructure.Services.Catalog;
using Xunit;

namespace ProjectK.Infrastructure.Tests.Services.Catalog;

public sealed class CatalogSyncBackgroundServiceTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "projectk-catalog-tests", Guid.NewGuid().ToString("N"));
    private readonly StubHandler _handler = new();
    private readonly CatalogStore _store = new();

    private string SnapshotPath => Path.Combine(_directory, "catalog.json");

    private CatalogSyncBackgroundService Service(string? krai = null, int refreshMinutes = 60)
    {
        var options = Options.Create(new CatalogOptions { BaseUrl = "https://catalog.test/", Krai = krai, RefreshMinutes = refreshMinutes, SnapshotPath = SnapshotPath });
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(f => f.CreateClient(CatalogClient.HttpClientName)).Returns(() => new HttpClient(_handler, disposeHandler: false));
        return new CatalogSyncBackgroundService(_store, new CatalogClient(factory.Object, options), options, _database.GetRequiredService<IServiceScopeFactory>(), NullLogger<CatalogSyncBackgroundService>.Instance);
    }

    private readonly ServiceProvider _database = Database(Guid.NewGuid().ToString());

    // The name is taken once: the options lambda runs for every scope, and a name made inside it
    // would hand each scope a database of its own.
    private static ServiceProvider Database(string name) => new ServiceCollection()
        .AddDbContext<AppDbContext>(db => db.UseInMemoryDatabase(name))
        .BuildServiceProvider();

    [Fact]
    public async Task Start_ShowsTheKraiNameTheTableHolds_NotTheSeededOne()
    {
        using (var scope = _database.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Krais.Add(new Krai { Code = "ua", Name = "Пласт в Україні" });
            await db.SaveChangesAsync();
        }

        _handler.Respond(HttpStatusCode.OK, CatalogTestData.Bytes, "\"r7\"");
        var service = Service();

        await service.StartAsync(CancellationToken.None);
        await service.StopAsync(CancellationToken.None);

        Assert.Equal("Пласт в Україні", ((IBadgesCatalog)_store).GetById("plavets")!.Country);
    }

    [Fact]
    public async Task Start_WithoutACopy_FetchesAndSavesOne()
    {
        _handler.Respond(HttpStatusCode.OK, CatalogTestData.Bytes, "\"r7\"");
        var service = Service(krai: "ua");

        await service.StartAsync(CancellationToken.None);
        await service.StopAsync(CancellationToken.None);

        Assert.Equal("https://catalog.test/v1/catalog?krai=ua", _handler.Requests[0].RequestUri!.ToString());
        Assert.Equal(2, ((IBadgesCatalog)_store).GetAll().Count);
        Assert.True(File.Exists(SnapshotPath));
        Assert.Equal("\"r7\"", File.ReadAllText(SnapshotPath + ".etag"));
    }

    [Fact]
    public async Task Start_WithACopy_ServesItWhenTheServiceIsDown()
    {
        Directory.CreateDirectory(_directory);
        File.WriteAllBytes(SnapshotPath, CatalogTestData.Bytes);
        _handler.Respond(HttpStatusCode.ServiceUnavailable);
        var service = Service();

        await service.StartAsync(CancellationToken.None);
        var refreshed = await service.RefreshAsync(CancellationToken.None);
        await service.StopAsync(CancellationToken.None);

        Assert.False(refreshed);
        Assert.Equal(7, _store.State.Revision);
        Assert.NotNull(((IProbesCatalog)_store).GetById("probe-1"));
    }

    [Fact]
    public async Task Refresh_SendsTheETag_AndKeepsTheCatalogueOn304()
    {
        _handler.Respond(HttpStatusCode.OK, CatalogTestData.Bytes, "\"r7\"");
        var service = Service();
        await service.RefreshAsync(CancellationToken.None);

        _handler.Respond(HttpStatusCode.NotModified);
        var refreshed = await service.RefreshAsync(CancellationToken.None);

        Assert.True(refreshed);
        Assert.Equal("\"r7\"", _handler.Requests[1].Headers.IfNoneMatch.Single().ToString());
        Assert.Equal(7, _store.State.Revision);
    }

    [Fact]
    public async Task Refresh_KeepsTheCatalogue_WhenTheAnswerIsEmpty()
    {
        _handler.Respond(HttpStatusCode.OK, CatalogTestData.Bytes);
        var service = Service();
        await service.RefreshAsync(CancellationToken.None);

        _handler.Respond(HttpStatusCode.OK, """{ "schemaVersion": 2, "revision": 8, "categories": [], "badges": [], "probes": [], "awards": [] }"""u8.ToArray());
        var refreshed = await service.RefreshAsync(CancellationToken.None);

        Assert.False(refreshed);
        Assert.Equal(7, _store.State.Revision);
    }

    [Fact]
    public async Task Start_WithoutACopyAndWithTheServiceDown_ServesTheBuiltInCopy()
    {
        _handler.Respond(HttpStatusCode.ServiceUnavailable);
        var service = Service();

        await service.StartAsync(CancellationToken.None);
        await service.StopAsync(CancellationToken.None);

        Assert.True(_store.IsLoaded);
        Assert.NotNull(((IProbesCatalog)_store).GetById("probe-1"));
        Assert.Null(_store.Metadata.ParsedAtUtc);
        Assert.False(File.Exists(SnapshotPath));
    }

    [Fact]
    public async Task NextDelay_OnTheBuiltInCopy_StartsAtAMinuteAndDoublesUpToTheInterval()
    {
        _handler.Respond(HttpStatusCode.ServiceUnavailable);
        var service = Service(refreshMinutes: 5);
        await service.StartAsync(CancellationToken.None);
        await service.StopAsync(CancellationToken.None);

        var delays = new List<TimeSpan> { service.NextDelay() };
        for (var i = 0; i < 4; i++)
        {
            await service.RefreshAsync(CancellationToken.None);
            delays.Add(service.NextDelay());
        }

        Assert.Equal([1, 2, 4, 5, 5], delays.Select(d => d.TotalMinutes));
    }

    [Fact]
    public async Task NextDelay_ReturnsToTheInterval_OnceTheServiceAnswers()
    {
        _handler.Respond(HttpStatusCode.ServiceUnavailable);
        var service = Service();
        await service.StartAsync(CancellationToken.None);
        await service.StopAsync(CancellationToken.None);
        await service.RefreshAsync(CancellationToken.None);

        _handler.Respond(HttpStatusCode.OK, CatalogTestData.Bytes, "\"r7\"");
        var refreshed = await service.RefreshAsync(CancellationToken.None);

        Assert.True(refreshed);
        Assert.Equal(7, _store.State.Revision);
        Assert.True(File.Exists(SnapshotPath));
        Assert.Equal(TimeSpan.FromMinutes(60), service.NextDelay());
    }

    [Fact]
    public async Task NextDelay_WithACopyOnDisk_KeepsTheIntervalWhileTheServiceIsDown()
    {
        Directory.CreateDirectory(_directory);
        File.WriteAllBytes(SnapshotPath, CatalogTestData.Bytes);
        _handler.Respond(HttpStatusCode.ServiceUnavailable);
        var service = Service();

        await service.StartAsync(CancellationToken.None);
        await service.StopAsync(CancellationToken.None);
        await service.RefreshAsync(CancellationToken.None);

        Assert.Equal(TimeSpan.FromMinutes(60), service.NextDelay());
    }

    public void Dispose()
    {
        _database.Dispose();
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    private sealed class StubHandler : HttpMessageHandler
    {
        private Func<HttpResponseMessage> _next = () => new HttpResponseMessage(HttpStatusCode.NotFound);

        public List<HttpRequestMessage> Requests { get; } = [];

        public void Respond(HttpStatusCode status, byte[]? body = null, string? etag = null)
        {
            _next = () =>
            {
                var response = new HttpResponseMessage(status) { Content = new ByteArrayContent(body ?? []) };
                if (etag is not null)
                {
                    response.Headers.ETag = EntityTagHeaderValue.Parse(etag);
                }

                return response;
            };
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            return Task.FromResult(_next());
        }
    }
}
