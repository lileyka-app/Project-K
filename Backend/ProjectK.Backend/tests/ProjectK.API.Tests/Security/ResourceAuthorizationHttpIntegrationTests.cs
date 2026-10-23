using System.Net;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using ProjectK.API.Authorization;
using ProjectK.API.Helpers;
using ProjectK.API.Tests.TestHelpers;
using ProjectK.Common.Interfaces.Modules.AuthModule;
using ProjectK.BusinessLogic.Modules.AuthModule.Services;
using ProjectK.BusinessLogic.Services.Caching;
using ProjectK.Common.Entities.KurinModule;
using ProjectK.Common.Extensions;
using ProjectK.Common.Interfaces;
using ProjectK.Common.Interfaces.Modules.InfrastructureModule;
using ProjectK.Common.Interfaces.Modules.KurinModule;
using ProjectK.Common.Models.Authorization;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Records;

namespace ProjectK.API.Tests.Security;

public class ResourceAuthorizationHttpIntegrationTests
{
    [Fact]
    public async Task SameKurinResource_ShouldReturn200()
    {
        var userKurinKey = Guid.NewGuid();
        var memberKey = Guid.NewGuid();

        await using var host = await ResourceGuardTestHost.StartAsync(userKurinKey, memberKey, userKurinKey);

        var response = await host.Client.GetAsync($"/api/test-resource/member/{memberKey}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task CrossKurinResource_ShouldReturn403()
    {
        var userKurinKey = Guid.NewGuid();
        var foreignKurinKey = Guid.NewGuid();
        var memberKey = Guid.NewGuid();

        await using var host = await ResourceGuardTestHost.StartAsync(userKurinKey, memberKey, foreignKurinKey);

        var response = await host.Client.GetAsync($"/api/test-resource/member/{memberKey}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    private sealed class ResourceGuardTestHost : IAsyncDisposable
    {
        private readonly WebApplication _app;

        private ResourceGuardTestHost(WebApplication app)
        {
            _app = app;
            Client = app.GetTestClient();
        }

        public HttpClient Client { get; }

        public static async Task<ResourceGuardTestHost> StartAsync(Guid userKurinKey, Guid memberKey, Guid memberKurinKey)
        {
            var builder = WebApplication.CreateBuilder(new WebApplicationOptions
            {
                EnvironmentName = "Testing"
            });

            builder.WebHost.UseTestServer();
            builder.Services.AddHttpContextAccessor();

            builder.Services.AddSingleton(new ResourceGuardAuthState("Member", Guid.NewGuid(), userKurinKey));

            builder.Services
                .AddAuthentication(options =>
                {
                    options.DefaultAuthenticateScheme = ResourceGuardAuthHandler.SchemeName;
                    options.DefaultChallengeScheme = ResourceGuardAuthHandler.SchemeName;
                })
                .AddScheme<AuthenticationSchemeOptions, ResourceGuardAuthHandler>(
                    ResourceGuardAuthHandler.SchemeName,
                    _ => { });

            builder.Services.AddAuthorization(options => options.AddProjectPolicies());

            var scopeReader = new Mock<IResourceScopeReader>();
            scopeReader
                .Setup(x => x.GetScopeAsync(ResourceType.Member, memberKey, It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ResourceScope(memberKurinKey, null, null));

            builder.Services.AddScoped(_ => scopeReader.Object);
            builder.Services.AddScoped<ICurrentUserContext, HttpCurrentUserContext>();
            builder.Services.AddScoped<IResourceAccessService, ResourceAccessService>();
            builder.Services.AddScoped<ICurrentPermissions, CurrentPermissions>();
            builder.Services.AddSingleton<IKurinAccessPolicyProvider>(new FixedKurinAccessPolicyProvider());
            builder.Services.AddMemoryCache();
            builder.Services.AddSingleton<IBackendCache, MemoryBackendCache>();

            builder.Services.AddControllers()
                .AddApplicationPart(typeof(ResourceAuthorizationHttpIntegrationTests).Assembly);

            var app = builder.Build();

            app.UseRouting();
            app.UseAuthentication();
            app.UseAuthorization();
            app.MapControllers();

            await app.StartAsync();
            return new ResourceGuardTestHost(app);
        }

        public async ValueTask DisposeAsync()
        {
            await _app.StopAsync();
            await _app.DisposeAsync();
        }
    }

    private sealed record ResourceGuardAuthState(string Role, Guid UserId, Guid KurinKey);

    private sealed class ResourceGuardAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        ResourceGuardAuthState authState)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        public const string SchemeName = "ResourceGuardTest";

        private readonly ResourceGuardAuthState _authState = authState;

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, _authState.UserId.ToString()),
                new("sub", _authState.UserId.ToString()),
                new("kurinKey", _authState.KurinKey.ToString()),
                new(ClaimTypes.Role, _authState.Role)
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

[ApiController]
[Route("api/test-resource")]
public class ResourceGuardProbeController : ControllerBase
{
    [Authorize(Policy = AuthorizationPolicies.RequireUser)]
    [HttpGet("member/{memberKey:guid}")]
    [ResourceAuthorize(ResourceType.Member, ResourceAction.Read, "route:memberKey")]
    public IActionResult GetMember(Guid memberKey)
    {
        return Ok(new { memberKey });
    }
}
