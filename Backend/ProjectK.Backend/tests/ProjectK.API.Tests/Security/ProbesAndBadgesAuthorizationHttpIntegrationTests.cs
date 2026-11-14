using System.Net;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using ProjectK.API.Authorization;
using ProjectK.API.Controllers.ProbesAndBadgesModule;
using ProjectK.BusinessLogic.Modules.ProbesAndBadgesModule.Features.Catalog;
using ProjectK.BusinessLogic.Modules.ProbesAndBadgesModule.Services;
using ProjectK.Common.Dtos.ProbesAndBadgesModule;
using ProjectK.Common.Models.Catalog;

namespace ProjectK.API.Tests.Security;

public class ProbesAndBadgesAuthorizationHttpIntegrationTests
{
    [Theory]
    [InlineData("/api/catalog/badges/meta")]
    [InlineData("/api/catalog/probes")]
    public async Task Anonymous_Request_ShouldReturn401(string route)
    {
        await using var host = await CatalogSecurityTestHost.StartAsync(roleClaim: null);

        var response = await host.Client.GetAsync(route);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData("/api/catalog/badges/meta")]
    [InlineData("/api/catalog/probes")]
    public async Task AuthenticatedUser_Request_ShouldReturn200(string route)
    {
        await using var host = await CatalogSecurityTestHost.StartAsync("Member");

        var response = await host.Client.GetAsync(route);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    /// <summary>
    /// The catalogues are reference data behind <c>RequireUser</c>, which means "authenticated" —
    /// the role is not consulted. This asserted 403 while the test host defined its own, stricter
    /// <c>RequireUser</c> that listed known roles; production never behaved that way, so the test was
    /// green about a rule the system did not have. Anonymous access is still refused, above.
    /// </summary>
    [Theory]
    [InlineData("/api/catalog/badges/meta")]
    [InlineData("/api/catalog/probes")]
    public async Task AuthenticatedWithUnknownRole_CanReadTheCatalogue(string route)
    {
        await using var host = await CatalogSecurityTestHost.StartAsync(roleClaim: "Guest");

        var response = await host.Client.GetAsync(route);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private sealed class CatalogSecurityTestHost : IAsyncDisposable
    {
        private readonly WebApplication _app;

        private CatalogSecurityTestHost(WebApplication app)
        {
            _app = app;
            Client = app.GetTestClient();
        }

        public HttpClient Client { get; }

        public static async Task<CatalogSecurityTestHost> StartAsync(string? roleClaim)
        {
            var badgesCatalogService = new Mock<IBadgesCatalogService>();
            var probesCatalogService = new Mock<IProbesCatalogService>();

            var testBadge = new Badge(
                Id: "badge-1",
                Title: "Badge 1",
                ImagePath: "https://catalog.example.invalid/v1/images/badge-1.png",
                Country: "UA",
                Specialization: "Default",
                Status: "active",
                Level: 1,
                LastUpdated: "2026-04-15",
                SeekerRequirements: "Do requirements",
                InstructorRequirements: "Confirm requirements",
                FixNotes: [],
                Requirements: [new BadgeRequirement("1", "Do requirements")],
                Author: string.Empty,
                Ulady: ["УПЮ"]);

            badgesCatalogService.Setup(x => x.GetBadgesMetadata())
                .Returns(new BadgesMetadata(
                    ParserVersion: "1.0",
                    ToolAuthor: "tests",
                    ParserComment: "catalog",
                    ParsedAtUtc: DateTimeOffset.UtcNow,
                    SourceUrl: "https://example.invalid",
                    FixerEnabled: false,
                    FixerMode: "none",
                    TotalBadges: 1));

            badgesCatalogService.Setup(x => x.GetBadges(It.IsAny<int>()))
                .Returns([testBadge]);

            badgesCatalogService.Setup(x => x.GetBadgeById(It.IsAny<string>()))
                .Returns(testBadge);

            probesCatalogService.Setup(x => x.GetProbes())
                .Returns([
                    new ProbeSummaryDto(
                        Id: "probe-1",
                        Title: "Probe 1",
                        PointsCount: 1,
                        SectionsCount: 0,
                        Edition: null,
                        Order: 0,
                        KraiCode: "",
                        StatusCode: "")
                ]);

            probesCatalogService.Setup(x => x.GetGroupedProbeById(It.IsAny<string>()))
                .Returns(new GroupedProbeDto(
                    Id: "probe-1",
                    Title: "Probe 1",
                    PointsCount: 1,
                    SectionsCount: 0,
                    Sections: []));

            var builder = WebApplication.CreateBuilder(new WebApplicationOptions
            {
                EnvironmentName = "Testing"
            });

            builder.WebHost.UseTestServer();

            builder.Services.AddSingleton(new CatalogAuthState(roleClaim));
            builder.Services
                .AddAuthentication(options =>
                {
                    options.DefaultAuthenticateScheme = CatalogAuthHandler.SchemeName;
                    options.DefaultChallengeScheme = CatalogAuthHandler.SchemeName;
                })
                .AddScheme<AuthenticationSchemeOptions, CatalogAuthHandler>(CatalogAuthHandler.SchemeName, _ => { });

            builder.Services.AddAuthorization(options => options.AddProjectPolicies());

            builder.Services.AddSingleton(badgesCatalogService.Object);
            builder.Services.AddSingleton(probesCatalogService.Object);
            builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(GetBadgesQuery).Assembly));

            builder.Services.AddControllers()
                .AddApplicationPart(typeof(BadgesCatalogController).Assembly);

            var app = builder.Build();

            app.UseRouting();
            app.UseAuthentication();
            app.UseAuthorization();
            app.MapControllers();

            await app.StartAsync();

            return new CatalogSecurityTestHost(app);
        }

        public async ValueTask DisposeAsync()
        {
            await _app.StopAsync();
            await _app.DisposeAsync();
        }
    }

    private sealed record CatalogAuthState(string? RoleClaim);

    private sealed class CatalogAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        CatalogAuthState authState)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        public const string SchemeName = "CatalogTest";

        private readonly CatalogAuthState _authState = authState;

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (string.IsNullOrWhiteSpace(_authState.RoleClaim))
            {
                return Task.FromResult(AuthenticateResult.NoResult());
            }

            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()),
                new(ClaimTypes.Role, _authState.RoleClaim)
            };

            var identity = new ClaimsIdentity(claims, SchemeName);
            var principal = new ClaimsPrincipal(identity);
            var ticket = new AuthenticationTicket(principal, SchemeName);

            return Task.FromResult(AuthenticateResult.Success(ticket));
        }

        protected override Task HandleChallengeAsync(AuthenticationProperties properties)
        {
            Response.StatusCode = StatusCodes.Status401Unauthorized;
            return Task.CompletedTask;
        }

        protected override Task HandleForbiddenAsync(AuthenticationProperties properties)
        {
            Response.StatusCode = StatusCodes.Status403Forbidden;
            return Task.CompletedTask;
        }
    }
}
