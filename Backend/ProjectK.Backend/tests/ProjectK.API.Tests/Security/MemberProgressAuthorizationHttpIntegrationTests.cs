using System.Net;
using System.Security.Claims;
using System.Text;
using System.Text.Encodings.Web;
using MediatR;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
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
using ProjectK.API.Tests.TestHelpers;
using ProjectK.Common.Interfaces.Modules.AuthModule;
using ProjectK.BusinessLogic.Modules.AuthModule.Services;
using ProjectK.BusinessLogic.Modules.ProbesAndBadgesModule.Features.Badge.Get;
using ProjectK.BusinessLogic.Modules.ProbesAndBadgesModule.Features.Badge.Review;
using ProjectK.BusinessLogic.Modules.ProbesAndBadgesModule.Features.Badge.Submit;
using ProjectK.BusinessLogic.Modules.ProbesAndBadgesModule.Features.Probe.Get;
using ProjectK.BusinessLogic.Modules.ProbesAndBadgesModule.Features.Probe.UpdateStatus;
using ProjectK.BusinessLogic.Modules.ProbesAndBadgesModule.Models;
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

public class MemberProgressAuthorizationHttpIntegrationTests
{
    [Theory]
    [InlineData("GET", "/api/member/{0}/badges/progress", null)]
    [InlineData("POST", "/api/member/{0}/badges/badge-1/submit", "{\"note\":\"submit\"}")]
    [InlineData("POST", "/api/member/{0}/badges/badge-1/review", "{\"isApproved\":true,\"note\":\"ok\"}")]
    [InlineData("GET", "/api/member/{0}/probes/probe-1/progress", null)]
    [InlineData("PUT", "/api/member/{0}/probes/probe-1/progress/status", "{\"status\":2,\"note\":\"done\"}")]
    public async Task Anonymous_Request_ShouldReturn401(string method, string routeTemplate, string? jsonBody)
    {
        var targetMemberKey = Guid.NewGuid();
        await using var host = await MemberProgressSecurityTestHost.StartAsync(
            role: null,
            userKurinKey: Guid.NewGuid(),
            targetMemberKey: targetMemberKey,
            targetMemberKurinKey: Guid.NewGuid(),
            currentUserGroupKey: Guid.NewGuid(),
            targetMemberGroupKey: Guid.NewGuid());

        var response = await SendAsync(host.Client, method, string.Format(routeTemplate, targetMemberKey), jsonBody);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData("POST", "/api/member/{0}/badges/badge-1/review", "{\"isApproved\":true,\"note\":\"ok\"}")]
    [InlineData("PUT", "/api/member/{0}/probes/probe-1/progress/status", "{\"status\":2,\"note\":\"done\"}")]
    public async Task UserRole_OnMentorOnlyEndpoint_ShouldReturn403(string method, string routeTemplate, string jsonBody)
    {
        var memberKey = Guid.NewGuid();
        var kurinKey = Guid.NewGuid();
        var groupKey = Guid.NewGuid();

        await using var host = await MemberProgressSecurityTestHost.StartAsync(
            role: "Member",
            userKurinKey: kurinKey,
            targetMemberKey: memberKey,
            targetMemberKurinKey: kurinKey,
            currentUserGroupKey: groupKey,
            targetMemberGroupKey: groupKey);

        var response = await SendAsync(host.Client, method, string.Format(routeTemplate, memberKey), jsonBody);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task CrossKurinMemberRead_ShouldReturn403()
    {
        var memberKey = Guid.NewGuid();

        await using var host = await MemberProgressSecurityTestHost.StartAsync(
            role: "Member",
            userKurinKey: Guid.NewGuid(),
            targetMemberKey: memberKey,
            targetMemberKurinKey: Guid.NewGuid(),
            currentUserGroupKey: Guid.NewGuid(),
            targetMemberGroupKey: Guid.NewGuid());

        var response = await host.Client.GetAsync($"/api/member/{memberKey}/badges/progress");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task SameKurinMemberRead_ShouldReturn200()
    {
        var memberKey = Guid.NewGuid();
        var kurinKey = Guid.NewGuid();
        var groupKey = Guid.NewGuid();

        await using var host = await MemberProgressSecurityTestHost.StartAsync(
            role: "Member",
            userKurinKey: kurinKey,
            targetMemberKey: memberKey,
            targetMemberKurinKey: kurinKey,
            currentUserGroupKey: groupKey,
            targetMemberGroupKey: groupKey);

        var response = await host.Client.GetAsync($"/api/member/{memberKey}/badges/progress");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Mentor_SameGroupReview_ShouldReturn200()
    {
        var memberKey = Guid.NewGuid();
        var kurinKey = Guid.NewGuid();
        var groupKey = Guid.NewGuid();

        await using var host = await MemberProgressSecurityTestHost.StartAsync(
            role: "KV.Vykhovnyk",
            userKurinKey: kurinKey,
            targetMemberKey: memberKey,
            targetMemberKurinKey: kurinKey,
            currentUserGroupKey: groupKey,
            targetMemberGroupKey: groupKey);

        var response = await SendAsync(
            host.Client,
            "POST",
            $"/api/member/{memberKey}/badges/badge-1/review",
            "{\"isApproved\":true,\"note\":\"ok\"}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Mentor_ForeignGroupReview_ShouldReturn403()
    {
        var memberKey = Guid.NewGuid();
        var kurinKey = Guid.NewGuid();

        await using var host = await MemberProgressSecurityTestHost.StartAsync(
            role: "KV.Vykhovnyk",
            userKurinKey: kurinKey,
            targetMemberKey: memberKey,
            targetMemberKurinKey: kurinKey,
            currentUserGroupKey: Guid.NewGuid(),
            targetMemberGroupKey: Guid.NewGuid());

        var response = await SendAsync(
            host.Client,
            "POST",
            $"/api/member/{memberKey}/badges/badge-1/review",
            "{\"isApproved\":false,\"note\":\"no\"}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    private static Task<HttpResponseMessage> SendAsync(HttpClient client, string method, string route, string? jsonBody)
    {
        if (string.Equals(method, "GET", StringComparison.OrdinalIgnoreCase))
        {
            return client.GetAsync(route);
        }

        var request = new HttpRequestMessage(new HttpMethod(method), route);
        if (!string.IsNullOrWhiteSpace(jsonBody))
        {
            request.Content = new StringContent(jsonBody, Encoding.UTF8, "application/json");
        }

        return client.SendAsync(request);
    }

    private sealed class MemberProgressSecurityTestHost : IAsyncDisposable
    {
        private readonly WebApplication _app;

        private MemberProgressSecurityTestHost(WebApplication app)
        {
            _app = app;
            Client = app.GetTestClient();
        }

        public HttpClient Client { get; }

        public static async Task<MemberProgressSecurityTestHost> StartAsync(
            string? role,
            Guid userKurinKey,
            Guid targetMemberKey,
            Guid targetMemberKurinKey,
            Guid currentUserGroupKey,
            Guid targetMemberGroupKey)
        {
            var builder = WebApplication.CreateBuilder(new WebApplicationOptions
            {
                EnvironmentName = "Testing"
            });

            builder.WebHost.UseTestServer();
            builder.Services.AddHttpContextAccessor();

            var userId = Guid.NewGuid();
            builder.Services.AddSingleton(new MemberProgressAuthState(role, userId, userKurinKey));

            builder.Services
                .AddAuthentication(options =>
                {
                    options.DefaultAuthenticateScheme = MemberProgressAuthHandler.SchemeName;
                    options.DefaultChallengeScheme = MemberProgressAuthHandler.SchemeName;
                })
                .AddScheme<AuthenticationSchemeOptions, MemberProgressAuthHandler>(
                    MemberProgressAuthHandler.SchemeName,
                    _ => { });

            builder.Services.AddAuthorization(options => options.AddProjectPolicies());

            var scopeReader = new Mock<IResourceScopeReader>();
            scopeReader
                .Setup(x => x.GetScopeAsync(ResourceType.Member, targetMemberKey, It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ResourceScope(targetMemberKurinKey, targetMemberGroupKey, Guid.NewGuid()));

            scopeReader
                .Setup(x => x.GetLedGroupKeysAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new[] { currentUserGroupKey });

            var mediator = new Mock<IMediator>();
            mediator
                .Setup(x => x.Send(It.IsAny<GetBadgeProgressesQuery>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ServiceResult<IEnumerable<BadgeProgressResponse>>(ResultType.Success, []));

            mediator
                .Setup(x => x.Send(It.IsAny<SubmitBadgeProgressCommand>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ServiceResult<BadgeProgressResponse>(
                    ResultType.Success,
                    new BadgeProgressResponse
                    {
                        BadgeProgressKey = Guid.NewGuid(),
                        MemberKey = targetMemberKey,
                        KurinKey = targetMemberKurinKey,
                        BadgeId = "badge-1",
                        Status = BadgeProgressStatus.Submitted
                    }));

            mediator
                .Setup(x => x.Send(It.IsAny<ReviewBadgeProgressCommand>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ServiceResult<BadgeProgressResponse>(
                    ResultType.Success,
                    new BadgeProgressResponse
                    {
                        BadgeProgressKey = Guid.NewGuid(),
                        MemberKey = targetMemberKey,
                        KurinKey = targetMemberKurinKey,
                        BadgeId = "badge-1",
                        Status = BadgeProgressStatus.Confirmed
                    }));

            mediator
                .Setup(x => x.Send(It.IsAny<GetProbeProgressQuery>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ServiceResult<ProbeProgressResponse>(
                    ResultType.Success,
                    ProbeProgressResponse.CreateNotStarted(targetMemberKey, targetMemberKurinKey, "probe-1")));

            mediator
                .Setup(x => x.Send(It.IsAny<UpdateProbeProgressStatusCommand>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ServiceResult<ProbeProgressResponse>(
                    ResultType.Success,
                    new ProbeProgressResponse
                    {
                        ProbeProgressKey = Guid.NewGuid(),
                        MemberKey = targetMemberKey,
                        KurinKey = targetMemberKurinKey,
                        ProbeId = "probe-1",
                        Status = ProbeProgressStatus.Completed
                    }));

            builder.Services.AddScoped(_ => scopeReader.Object);
            builder.Services.AddSingleton(mediator.Object);
            builder.Services.AddScoped<ICurrentUserContext, HttpCurrentUserContext>();
            builder.Services.AddScoped<IResourceAccessService, ResourceAccessService>();
            builder.Services.AddScoped<ICurrentPermissions, CurrentPermissions>();
            builder.Services.AddSingleton<IKurinAccessPolicyProvider>(new FixedKurinAccessPolicyProvider());
            builder.Services.AddMemoryCache();
            builder.Services.AddSingleton<IBackendCache, MemoryBackendCache>();

            builder.Services.AddControllers()
                .AddApplicationPart(typeof(MemberProgressController).Assembly);

            var app = builder.Build();

            app.UseRouting();
            app.UseAuthentication();
            app.UseAuthorization();
            app.MapControllers();

            await app.StartAsync();
            return new MemberProgressSecurityTestHost(app);
        }

        public async ValueTask DisposeAsync()
        {
            await _app.StopAsync();
            await _app.DisposeAsync();
        }
    }

    private sealed record MemberProgressAuthState(string? Role, Guid UserId, Guid KurinKey);

    private sealed class MemberProgressAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        MemberProgressAuthState authState)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        public const string SchemeName = "MemberProgressSecurityTest";

        private readonly MemberProgressAuthState _authState = authState;

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (_authState.Role is null)
            {
                return Task.FromResult(AuthenticateResult.NoResult());
            }

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
