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
using ProjectK.API.Authorization;
using ProjectK.API.Controllers.AuthModule;
using ProjectK.Common.Models.Authorization;

namespace ProjectK.API.Tests.Security;

public class AuthorizationHttpIntegrationTests
{
    [Theory]
    [InlineData("/api/member/{0}")]
    [InlineData("/api/group/{0}")]
    [InlineData("/api/kurin/{0}")]
    public async Task Anonymous_RequestToProtectedEndpoint_ShouldReturn401(string routeTemplate)
    {
        await using var host = await SecurityTestHost.StartAsync();
        var route = string.Format(routeTemplate, Guid.NewGuid());

        var response = await host.Client.GetAsync(route);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// <summary>
    /// Endpoints a plain member is refused by the <b>policy</b>, before any record is looked at.
    /// <para>
    /// Planning used to be listed here. It is no longer decided by a policy: every member carries
    /// <c>PlanningSession:Read:KurinWide</c>, so reading is now settled by the resource check, which
    /// keeps a member inside their own kurin. That planning still requires authentication is pinned
    /// by <see cref="ProtectedEndpointsAnonymousHttpIntegrationTests"/>.
    /// </para>
    /// </summary>
    [Theory]
    [InlineData(SystemRole.Member, "/api/user/users")]
    public async Task AuthenticatedUser_WithInsufficientRole_ShouldReturn403(string roleClaim, string routeTemplate)
    {
        await using var host = await SecurityTestHost.StartAsync(roleClaim);
        var route = routeTemplate.Contains("{0}")
            ? string.Format(routeTemplate, Guid.NewGuid())
            : routeTemplate;

        var response = await host.Client.GetAsync(route);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    private sealed class SecurityTestHost : IAsyncDisposable
    {
        private readonly WebApplication _app;

        private SecurityTestHost(WebApplication app)
        {
            _app = app;
            Client = app.GetTestClient();
        }

        public HttpClient Client { get; }

        public static async Task<SecurityTestHost> StartAsync(string? roleClaim = null)
        {
            var builder = WebApplication.CreateBuilder(new WebApplicationOptions
            {
                EnvironmentName = "Testing"
            });

            builder.WebHost.UseTestServer();
            builder.Services.AddSingleton(new TestAuthState(roleClaim));

            builder.Services
                .AddAuthentication(options =>
                {
                    options.DefaultAuthenticateScheme = TestAuthHandler.SchemeName;
                    options.DefaultChallengeScheme = TestAuthHandler.SchemeName;
                })
                .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(
                    TestAuthHandler.SchemeName,
                    _ => { });

            builder.Services.AddAuthorization(options => options.AddProjectPolicies());

            builder.Services.AddControllers()
                .AddApplicationPart(typeof(AuthController).Assembly);

            var app = builder.Build();

            app.UseRouting();
            app.UseAuthentication();
            app.UseAuthorization();
            app.MapControllers();

            await app.StartAsync();
            return new SecurityTestHost(app);
        }

        public async ValueTask DisposeAsync()
        {
            await _app.StopAsync();
            await _app.DisposeAsync();
        }
    }

    private sealed record TestAuthState(string? RoleClaim);

    private sealed class TestAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        TestAuthState authState)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        public const string SchemeName = "Test";

        private readonly TestAuthState _authState = authState;

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (_authState.RoleClaim is null)
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
