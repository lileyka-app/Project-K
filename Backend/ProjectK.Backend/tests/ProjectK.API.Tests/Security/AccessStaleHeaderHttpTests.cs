using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using AutoMapper;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Moq;
using ProjectK.API.Middleware;
using ProjectK.BusinessLogic.Modules.AuthModule.Services;
using ProjectK.BusinessLogic.Modules.KurinModule.Features.Leadership.Upsert;
using ProjectK.BusinessLogic.Modules.KurinModule.Models;
using ProjectK.BusinessLogic.Services.Caching;
using ProjectK.Common.Entities.KurinModule;
using ProjectK.Common.Interfaces;
using ProjectK.Common.Interfaces.Modules.AuthModule;
using ProjectK.Common.Interfaces.Modules.InfrastructureModule;
using ProjectK.Common.Interfaces.Modules.KurinModule;
using ProjectK.Common.Interfaces.Modules.MemberModule;
using ProjectK.Common.Models.Authorization;
using ProjectK.Common.Models.Dtos;
using ProjectK.Common.Models.Dtos.KurinModule;
using ProjectK.Common.Models.Dtos.KurinModule.Requests;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Records;
using ProjectK.Infrastructure.Services.JwtService;

namespace ProjectK.API.Tests.Security;

/// <summary>
/// An office changed, and the people it changed for hear about it on their very next request:
/// tokens from <see cref="JwtService"/>, read by the real bearer handler, judged by the middleware
/// against marks the real leadership handler left.
/// </summary>
public class AccessStaleHeaderHttpTests
{
    private const string Key = "unit-test-signing-key-that-is-long-enough-for-hmac-sha256";
    private const string Issuer = "lileyka-tests";
    private const string Audience = "lileyka";

    [Fact]
    public async Task AfterAnOfficeChanges_OnlyItsHoldersAreToldTheirTokenIsStale()
    {
        await using var host = await StaleHost.StartAsync();
        var seated = Guid.NewGuid();
        var bystander = Guid.NewGuid();
        var seatedToken = MintedAMinuteAgo(seated);
        var bystanderToken = MintedAMinuteAgo(bystander);

        var before = await host.GetAsync(seatedToken);
        await SeatAsync(host.Signal, memberUserKey: seated);
        var seatedAfter = await host.GetAsync(seatedToken);
        var bystanderAfter = await host.GetAsync(bystanderToken);
        var refreshed = await host.GetAsync(MintedNow(seated));

        Assert.False(before.Headers.Contains(AccessStaleHeaderMiddleware.HeaderName));
        Assert.Equal("1", Assert.Single(seatedAfter.Headers.GetValues(AccessStaleHeaderMiddleware.HeaderName)));
        Assert.False(bystanderAfter.Headers.Contains(AccessStaleHeaderMiddleware.HeaderName));
        Assert.False(refreshed.Headers.Contains(AccessStaleHeaderMiddleware.HeaderName));
    }

    [Fact]
    public async Task AfterAKurinPolicyIsSaved_EveryTokenOfThatKurinIsStale()
    {
        await using var host = await StaleHost.StartAsync();
        var kurin = Guid.NewGuid();
        var inKurin = MintedAMinuteAgo(Guid.NewGuid(), kurin);
        var elsewhere = MintedAMinuteAgo(Guid.NewGuid(), Guid.NewGuid());

        host.Signal.MarkKurin(kurin);

        Assert.True((await host.GetAsync(inKurin)).Headers.Contains(AccessStaleHeaderMiddleware.HeaderName));
        Assert.False((await host.GetAsync(elsewhere)).Headers.Contains(AccessStaleHeaderMiddleware.HeaderName));
    }

    [Fact]
    public async Task AnAnonymousRequest_IsNeverMarked()
    {
        await using var host = await StaleHost.StartAsync();
        host.Signal.MarkUser(Guid.NewGuid());

        var response = await host.GetAsync(token: null);

        Assert.False(response.Headers.Contains(AccessStaleHeaderMiddleware.HeaderName));
    }

    /// <summary>Seats the member behind <paramref name="memberUserKey"/> as курінний, through the real handler.</summary>
    private static async Task SeatAsync(IAccessChangeSignal signal, Guid memberUserKey)
    {
        var memberKey = Guid.NewGuid();
        var kurinKey = Guid.NewGuid();
        var leadership = new Leadership { LeadershipKey = Guid.NewGuid(), Type = LeadershipType.Kurin, KurinKey = kurinKey };

        var leaderships = new Mock<ILeadershipRepository>();
        leaderships.Setup(r => r.GetByKeyAsync(leadership.LeadershipKey, It.IsAny<CancellationToken>())).ReturnsAsync(leadership);
        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork.SetupGet(u => u.Leaderships).Returns(leaderships.Object);
        unitOfWork.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var mapper = new Mock<IMapper>();
        mapper.Setup(m => m.Map<LeadershipResponse>(It.IsAny<Leadership>())).Returns(new LeadershipResponse());

        var assigner = new Mock<ICurrentUserContext>();
        assigner.SetupGet(c => c.Roles).Returns([SystemRole.ForOffice(LeadershipType.KV, LeadershipRole.Zvyazkovyi)]);

        var members = new Mock<IMemberDirectory>();
        members.Setup(m => m.GetByKeysAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([new MemberSummary(memberKey, memberUserKey, kurinKey, null, "A", "B", "a@example.com", null)]);

        var handler = new UpsertLeadershipCommandHandler(
            unitOfWork.Object, mapper.Object, assigner.Object, members.Object, new Mock<IBackendCache>().Object, signal, new Mock<IKurinAudit>().Object);
        var request = new UpsertLeadershipRequest
        {
            Type = "kurin",
            EntityKey = kurinKey,
            LeadershipHistories =
            [
                new LeadershipHistoryMemberDto
                {
                    Role = LeadershipRole.Kurinnuy.ToString(),
                    Member = new MemberLookupDto { MemberKey = memberKey, FirstName = "A", LastName = "B" }
                }
            ]
        };

        var result = await handler.Handle(new UpsertLeadershipCommand(request, leadership.LeadershipKey), CancellationToken.None);
        Assert.Equal(ResultType.Success, result.Type);
    }

    private static string MintedAMinuteAgo(Guid userKey, Guid? kurinKey = null) =>
        Jwt(new StoppedClock(DateTimeOffset.UtcNow.AddMinutes(-1))).GenerateAccessToken(
            userKey.ToString(), "u@example.com", [SystemRole.Member], kurinKey?.ToString(), hasSecondFactor: false);

    private static string MintedNow(Guid userKey) =>
        Jwt(TimeProvider.System).GenerateAccessToken(userKey.ToString(), "u@example.com", [SystemRole.Member], null, hasSecondFactor: false);

    private static JwtService Jwt(TimeProvider clock) => new(Configuration(), clock);

    private static IConfiguration Configuration() =>
        new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Jwt:Key"] = Key,
            ["Jwt:Issuer"] = Issuer,
            ["Jwt:Audience"] = Audience,
            ["Jwt:ExpiresInMinutes"] = "5"
        }).Build();

    private sealed class StoppedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    /// <summary>The bearer handler as Program.cs configures it, the middleware behind it, one endpoint.</summary>
    private sealed class StaleHost : IAsyncDisposable
    {
        private readonly WebApplication _app;
        private readonly HttpClient _client;

        private StaleHost(WebApplication app)
        {
            _app = app;
            _client = app.GetTestClient();
        }

        public IAccessChangeSignal Signal => _app.Services.GetRequiredService<IAccessChangeSignal>();

        public static async Task<StaleHost> StartAsync()
        {
            var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Testing" });
            builder.WebHost.UseTestServer();
            builder.Configuration.AddConfiguration(Configuration());
            builder.Services.AddSingleton(TimeProvider.System);
            builder.Services.AddSingleton<IAccessChangeSignal, AccessChangeSignal>();

            builder.Services
                .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                .AddJwtBearer(options => options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = Issuer,
                    ValidAudience = Audience,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Key)),
                    RoleClaimType = ClaimTypes.Role,
                    NameClaimType = "sub"
                });

            var app = builder.Build();
            app.UseAuthentication();
            app.UseMiddleware<AccessStaleHeaderMiddleware>();
            app.MapGet("/api/probe", () => Results.Ok());
            await app.StartAsync();
            return new StaleHost(app);
        }

        public Task<HttpResponseMessage> GetAsync(string? token)
        {
            var request = new HttpRequestMessage(HttpMethod.Get, "/api/probe");
            if (token is not null)
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            }

            return _client.SendAsync(request);
        }

        public async ValueTask DisposeAsync()
        {
            _client.Dispose();
            await _app.StopAsync();
            await _app.DisposeAsync();
        }
    }
}
