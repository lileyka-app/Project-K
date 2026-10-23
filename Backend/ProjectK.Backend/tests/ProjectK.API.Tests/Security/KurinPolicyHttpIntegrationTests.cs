using System.Net;
using System.Security.Claims;
using System.Text;
using System.Text.Encodings.Web;
using MediatR;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using ProjectK.API.Authorization;
using ProjectK.API.Controllers.ProbesAndBadgesModule;
using ProjectK.API.Helpers;
using ProjectK.BusinessLogic.Modules.AuthModule.Services;
using ProjectK.BusinessLogic.Modules.KurinModule.Features.Kurin.AccessPolicy;
using ProjectK.BusinessLogic.Modules.ProbesAndBadgesModule.Features.Probe.UpdatePointSignature;
using ProjectK.BusinessLogic.Modules.ProbesAndBadgesModule.Models;
using ProjectK.BusinessLogic.Services.Caching;
using ProjectK.Common.Entities.KurinModule;
using ProjectK.Common.Interfaces;
using ProjectK.Common.Interfaces.Modules.AuthModule;
using ProjectK.Common.Interfaces.Modules.InfrastructureModule;
using ProjectK.Common.Interfaces.Modules.KurinModule;
using ProjectK.Common.Models.Authorization;
using ProjectK.Common.Models.Authorization.KurinPolicy;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Records;

namespace ProjectK.API.Tests.Security;

/// <summary>
/// The kurin's policy decides at the endpoint, through the real provider and its cache: a cell
/// saved in the settings is met by the very next request, with the same token.
/// </summary>
public class KurinPolicyHttpIntegrationTests
{
    private static readonly string Vykhovnyk = SystemRole.ForOffice(LeadershipType.KV, LeadershipRole.Vykhovnyk);

    [Fact]
    public async Task Unsigning_ShouldBeRefused_WhenTheKurinKeepsUnsignProbe_WhileSigningStillWorks()
    {
        await using var host = await PolicyTestHost.StartAsync(Vykhovnyk);
        await host.SaveAsync(KurinCapability.UnsignProbe, PolicyAudience.Vykhovnyk, CapabilityReach.None);

        var unsign = await host.PutAsync($"/api/member/{host.MemberKey}/probes/probe-1/points/p-1/unsign");
        var sign = await host.PutAsync($"/api/member/{host.MemberKey}/probes/probe-1/points/p-1/sign");

        Assert.Equal(HttpStatusCode.Forbidden, unsign.StatusCode);
        Assert.Equal(HttpStatusCode.OK, sign.StatusCode);
    }

    [Fact]
    public async Task APolicySaved_ShouldBeMetByTheNextRequest_WithTheSameToken()
    {
        await using var host = await PolicyTestHost.StartAsync(Vykhovnyk);
        var route = $"/api/member/{host.MemberKey}/probes/probe-1/points/p-1/unsign";

        var before = await host.PutAsync(route);

        // Written behind the provider's back the change waits for the cache; saved the way the
        // settings page saves it, it drops the cache and is met at once.
        host.Kurin.AccessPolicy = PolicyOf(KurinCapability.UnsignProbe, PolicyAudience.Vykhovnyk, CapabilityReach.None).Write();
        var cached = await host.PutAsync(route);
        await host.SaveAsync(KurinCapability.UnsignProbe, PolicyAudience.Vykhovnyk, CapabilityReach.None);
        var after = await host.PutAsync(route);

        Assert.Equal(HttpStatusCode.OK, before.StatusCode);
        Assert.Equal(HttpStatusCode.OK, cached.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, after.StatusCode);
    }

    [Fact]
    public async Task APolicyGivingMore_ShouldOpenTheEndpoint_ToAnOfficeTheStaticMapLeavesOut()
    {
        var hurtkovyy = SystemRole.ForOffice(LeadershipType.Group, LeadershipRole.Hurtkoviy);
        await using var host = await PolicyTestHost.StartAsync(hurtkovyy);
        var route = $"/api/member/{host.MemberKey}/probes/probe-1/points/p-1/sign";

        var before = await host.PutAsync(route);
        await host.SaveAsync(KurinCapability.SignProbe, PolicyAudience.Hurtkovyy, CapabilityReach.OwnGroup);
        var after = await host.PutAsync(route);

        Assert.Equal(HttpStatusCode.Forbidden, before.StatusCode);
        Assert.Equal(HttpStatusCode.OK, after.StatusCode);
    }

    private static KurinAccessPolicy PolicyOf(KurinCapability capability, PolicyAudience audience, CapabilityReach reach) =>
        KurinAccessPolicy.FromMatrix(Matrix(capability, audience, reach));

    private static Dictionary<string, IReadOnlyDictionary<string, string?>> Matrix(
        KurinCapability capability, PolicyAudience audience, CapabilityReach reach) =>
        new() { [capability.ToString()] = new Dictionary<string, string?> { [audience.ToString()] = reach.ToString() } };

    private sealed class PolicyTestHost : IAsyncDisposable
    {
        private readonly WebApplication _app;

        private PolicyTestHost(WebApplication app, Kurin kurin, Guid memberKey)
        {
            _app = app;
            Kurin = kurin;
            MemberKey = memberKey;
            Client = app.GetTestClient();
        }

        public HttpClient Client { get; }

        public Kurin Kurin { get; }

        public Guid MemberKey { get; }

        public Task<HttpResponseMessage> PutAsync(string route) =>
            Client.PutAsync(route, new StringContent("{}", Encoding.UTF8, "application/json"));

        /// <summary>Saves one cell the way «Права й видимість» does, through the real command.</summary>
        public async Task SaveAsync(KurinCapability capability, PolicyAudience audience, CapabilityReach reach)
        {
            using var scope = _app.Services.CreateScope();
            var handler = new SetKurinAccessPolicyCommandHandler(
                scope.ServiceProvider.GetRequiredService<IUnitOfWork>(),
                scope.ServiceProvider.GetRequiredService<IKurinAccessPolicyProvider>(),
                new Mock<IAccessChangeSignal>().Object);
            var result = await handler.Handle(
                new SetKurinAccessPolicyCommand(Kurin.KurinKey, Matrix(capability, audience, reach)), CancellationToken.None);
            Assert.Equal(ResultType.Success, result.Type);
        }

        public static async Task<PolicyTestHost> StartAsync(string role)
        {
            var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Testing" });
            builder.WebHost.UseTestServer();
            builder.Services.AddHttpContextAccessor();

            var kurin = new Kurin(1);
            var groupKey = Guid.NewGuid();
            var memberKey = Guid.NewGuid();
            builder.Services.AddSingleton(new PolicyAuthState(role, Guid.NewGuid(), kurin.KurinKey));

            builder.Services
                .AddAuthentication(options =>
                {
                    options.DefaultAuthenticateScheme = PolicyAuthHandler.SchemeName;
                    options.DefaultChallengeScheme = PolicyAuthHandler.SchemeName;
                })
                .AddScheme<AuthenticationSchemeOptions, PolicyAuthHandler>(PolicyAuthHandler.SchemeName, _ => { });
            builder.Services.AddAuthorization(options => options.AddProjectPolicies());

            var scopeReader = new Mock<IResourceScopeReader>();
            scopeReader
                .Setup(x => x.GetScopeAsync(ResourceType.Member, memberKey, It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ResourceScope(kurin.KurinKey, groupKey, Guid.NewGuid()));
            scopeReader
                .Setup(x => x.GetLedGroupKeysAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new[] { groupKey });

            var kurins = new Mock<IKurinRepository>();
            kurins.Setup(x => x.GetByKeyAsync(kurin.KurinKey, It.IsAny<CancellationToken>())).ReturnsAsync(kurin);
            var unitOfWork = new Mock<IUnitOfWork>();
            unitOfWork.SetupGet(x => x.Kurins).Returns(kurins.Object);
            unitOfWork.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

            var mediator = new Mock<IMediator>();
            mediator
                .Setup(x => x.Send(It.IsAny<UpdateProbePointSignatureCommand>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ServiceResult<ProbeProgressResponse>(
                    ResultType.Success,
                    ProbeProgressResponse.CreateNotStarted(memberKey, kurin.KurinKey, "probe-1")));

            builder.Services.AddSingleton(unitOfWork.Object);
            builder.Services.AddScoped(_ => scopeReader.Object);
            builder.Services.AddSingleton(mediator.Object);
            builder.Services.AddScoped<ICurrentUserContext, HttpCurrentUserContext>();
            builder.Services.AddScoped<IResourceAccessService, ResourceAccessService>();
            builder.Services.AddScoped<ICurrentPermissions, CurrentPermissions>();
            builder.Services.AddScoped<IKurinAccessPolicyProvider, KurinAccessPolicyProvider>();
            builder.Services.AddMemoryCache();
            builder.Services.AddSingleton<IBackendCache, MemoryBackendCache>();
            builder.Services.AddControllers().AddApplicationPart(typeof(MemberProgressController).Assembly);

            var app = builder.Build();
            app.UseRouting();
            app.UseAuthentication();
            app.UseAuthorization();
            app.MapControllers();

            await app.StartAsync();
            return new PolicyTestHost(app, kurin, memberKey);
        }

        public async ValueTask DisposeAsync()
        {
            await _app.StopAsync();
            await _app.DisposeAsync();
        }
    }

    private sealed record PolicyAuthState(string Role, Guid UserId, Guid KurinKey);

    private sealed class PolicyAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        PolicyAuthState authState)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        public const string SchemeName = "KurinPolicyTest";

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, authState.UserId.ToString()),
                new("sub", authState.UserId.ToString()),
                new("kurinKey", authState.KurinKey.ToString()),
                new(ClaimTypes.Role, SystemRole.Member),
                new(ClaimTypes.Role, authState.Role)
            };

            var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, SchemeName));
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(principal, SchemeName)));
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
