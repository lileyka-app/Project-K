using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using ProjectK.Common.Entities.AuthModule;
using ProjectK.Common.Entities.KurinModule;
using ProjectK.Common.Models.Settings;
using ProjectK.Infrastructure.DbContexts;
using ProjectK.Infrastructure.Services.Telemetry;
using Xunit;

namespace ProjectK.Infrastructure.Tests.Services.Telemetry;

public class TelemetryTests
{
    private static DbContextOptions<AppDbContext> NewDatabase() =>
        new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;

    [Theory]
    [InlineData("Production", "prod")]
    [InlineData("SelfHost", "selfhost")]
    [InlineData("Staging", "staging")]
    [InlineData("My Lab_1", "my-lab-1")]
    [InlineData("1st", "other")]
    public void Kind_ShouldBeAShortLowercaseCode(string environment, string expected)
    {
        Assert.Equal(expected, TelemetryCollector.Kind(environment));
    }

    [Theory]
    [InlineData("true", true)]
    [InlineData("True", true)]
    [InlineData(" 1 ", true)]
    [InlineData("yes", true)]
    [InlineData("ON", true)]
    [InlineData("false", false)]
    [InlineData("0", false)]
    [InlineData("no", false)]
    [InlineData("off", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    [InlineData("maybe", false)]
    public void Enabled_IsOnOnlyForAClearOn_AndBindsWithoutThrowing(string? value, bool expected)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Telemetry:Enabled"] = value })
            .Build();

        var options = configuration.GetSection(TelemetryOptions.SectionName).Get<TelemetryOptions>() ?? new TelemetryOptions();

        Assert.Equal(expected, options.IsEnabled);
    }

    [Theory]
    [InlineData("v1.1.9", "v1.2.0", true)]
    [InlineData("v1.9.0", "v1.10.0", true)]
    [InlineData("v1.2.0", "v1.2.0", false)]
    [InlineData("1.1.9", "v1.2", true)]
    [InlineData("1.1", "v1.2", true)]
    [InlineData("1.2", "v1.2.0", false)]
    [InlineData("v0.0.0-dev", "v1.2.0", false)]
    [InlineData("v1.2.0", "nightly", false)]
    public void IsOlder_ShouldCompareReleasesByNumber(string version, string latest, bool expected)
    {
        Assert.Equal(expected, TelemetryBackgroundService.IsOlder(version, latest));
    }

    [Fact]
    public void Counters_ShouldStartAgainFromZero_AfterAPing()
    {
        var counters = new TelemetryCounters();
        counters.Count(200);
        counters.Count(503);
        counters.Count(404);

        Assert.Equal((3L, 1L), counters.TakeAndReset());
        Assert.Equal((0L, 0L), counters.TakeAndReset());
    }

    [Fact]
    public async Task InstanceId_ShouldBeMadeOnce_AndKept()
    {
        var options = NewDatabase();
        string first;
        await using (var db = new AppDbContext(options))
        {
            first = await new TelemetryCollector(db).InstanceIdAsync(CancellationToken.None);
        }

        await using (var db = new AppDbContext(options))
        {
            Assert.Equal(first, await new TelemetryCollector(db).InstanceIdAsync(CancellationToken.None));
        }

        Assert.True(Guid.TryParse(first, out _));
    }

    [Fact]
    public async Task Collect_ShouldCountKurinsMembersKraisAndActiveUsers()
    {
        var now = new DateTime(2026, 10, 16, 12, 0, 0, DateTimeKind.Utc);
        await using var db = new AppDbContext(NewDatabase());
        db.Kurins.AddRange(new Kurin(1) { KraiCode = "cz" }, new Kurin(2) { KraiCode = "cz" }, new Kurin(3) { KraiCode = "UA" });
        db.Members.AddRange(Member("Ірина"), Member("Олег"));
        var recent = Guid.NewGuid();
        var monthly = Guid.NewGuid();
        db.UserRefreshTokens.AddRange(
            Session(recent, now.AddDays(-1)),
            Session(recent, now.AddDays(-2)),
            Session(monthly, now.AddDays(-20)),
            Session(Guid.NewGuid(), now.AddDays(-40)));
        await db.SaveChangesAsync();

        var report = await new TelemetryCollector(db).CollectAsync("id", "v1.2.0", "SelfHost", (120, 2), TimeSpan.FromHours(30.5), now, CancellationToken.None);

        Assert.Equal(["cz", "ua"], report.Krais);
        Assert.Equal(3, report.Kurins);
        Assert.Equal(2, report.Members);
        Assert.Equal(1, report.ActiveUsers7d);
        Assert.Equal(2, report.ActiveUsers30d);
        Assert.Equal(120, report.Requests24h);
        Assert.Equal(2, report.Errors24h);
        Assert.Equal(30, report.UptimeHours);
        Assert.Equal("selfhost", report.Kind);
    }

    [Fact]
    public async Task Ping_ShouldPostTheReportInTheWorkersShape()
    {
        var options = NewDatabase();
        var handler = new RecordingHandler("""{"ok":true,"latest":"v1.3.0"}""");
        var service = NewService(options, handler, "v1.2.0");

        await service.PingAsync(CancellationToken.None);

        Assert.Equal("https://status.test/v1/ping", handler.Url);
        using var body = JsonDocument.Parse(handler.Body!);
        var root = body.RootElement;
        Assert.Equal("v1.2.0", root.GetProperty("version").GetString());
        Assert.Equal("selfhost", root.GetProperty("kind").GetString());
        Assert.True(Guid.TryParse(root.GetProperty("instanceId").GetString(), out _));
        foreach (var field in new[] { "krais", "kurins", "members", "activeUsers7d", "activeUsers30d", "requests24h", "errors24h", "uptimeHours" })
        {
            Assert.True(root.TryGetProperty(field, out _), field);
        }
    }

    [Fact]
    public async Task Ping_ShouldNotThrow_WhenTheServiceIsDown()
    {
        var service = NewService(NewDatabase(), new RecordingHandler(null), "v1.2.0");

        await service.PingAsync(CancellationToken.None);
    }

    private static TelemetryBackgroundService NewService(DbContextOptions<AppDbContext> options, RecordingHandler handler, string version)
    {
        var services = new ServiceCollection();
        services.AddScoped(_ => new AppDbContext(options));
        services.AddScoped<TelemetryCollector>();
        var provider = services.BuildServiceProvider();

        var httpClients = new Mock<IHttpClientFactory>();
        httpClients.Setup(f => f.CreateClient(TelemetryBackgroundService.HttpClientName)).Returns(() => new HttpClient(handler));
        var environment = new Mock<IHostEnvironment>();
        environment.SetupGet(e => e.EnvironmentName).Returns("SelfHost");
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["ReleaseInfo:Version"] = version }).Build();

        return new TelemetryBackgroundService(
            provider,
            httpClients.Object,
            new TelemetryCounters(),
            Options.Create(new TelemetryOptions { Enabled = "true", Endpoint = "https://status.test/v1/ping" }),
            configuration,
            environment.Object,
            NullLogger<TelemetryBackgroundService>.Instance);
    }

    private static Member Member(string firstName) => new() { FirstName = firstName, LastName = "Тест", Email = $"{Guid.NewGuid():N}@example.com", PhoneNumber = "+420000000000" };

    private static UserRefreshToken Session(Guid userId, DateTime created) =>
        new() { UserId = userId, Token = Guid.NewGuid().ToString(), ExpiresAtUtc = created.AddDays(30), CreatedDate = created };

    private sealed class RecordingHandler(string? answer) : HttpMessageHandler
    {
        public string? Url { get; private set; }
        public string? Body { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Url = request.RequestUri?.ToString();
            Body = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
            if (answer is null)
            {
                throw new HttpRequestException("connection refused");
            }

            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(answer, System.Text.Encoding.UTF8, "application/json") };
        }
    }
}
