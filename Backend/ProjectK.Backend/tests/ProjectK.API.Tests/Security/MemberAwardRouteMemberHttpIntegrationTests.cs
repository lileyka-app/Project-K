using System.Net;
using System.Security.Claims;
using System.Text;
using System.Text.Encodings.Web;
using AutoMapper;
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
using ProjectK.API.Controllers.KurinModule;
using ProjectK.API.Helpers;
using ProjectK.API.Tests.TestHelpers;
using ProjectK.BusinessLogic.Modules.AuthModule.Services;
using ProjectK.BusinessLogic.Modules.KurinModule.Features.MemberAward.Delete;
using ProjectK.BusinessLogic.Modules.KurinModule.Features.MemberAward.Review;
using ProjectK.BusinessLogic.Services.Caching;
using ProjectK.Common.Entities.KurinModule;
using ProjectK.Common.Interfaces;
using ProjectK.Common.Interfaces.Modules.AuthModule;
using ProjectK.Common.Interfaces.Modules.InfrastructureModule;
using ProjectK.Common.Interfaces.Modules.KurinModule;
using ProjectK.Common.Interfaces.Modules.MemberModule;
using ProjectK.Common.Interfaces.Modules.ProbesAndBadgesModule;
using ProjectK.Common.Models.Dtos.KurinModule;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Records;

namespace ProjectK.API.Tests.Security;

/// <summary>
/// The resource check on the award endpoints authorizes the member in the route, so the award
/// itself must belong to that member. Otherwise a впорядник puts his own youth's key in the route
/// and confirms an award of someone outside his гурток, and any member puts his own key there and
/// deletes anyone's unconfirmed award.
/// </summary>
public class MemberAwardRouteMemberHttpIntegrationTests
{
    [Theory]
    [InlineData("KV.Vykhovnyk", "POST", "/api/member/{0}/awards/{1}/review", "{\"isApproved\":true}")]
    [InlineData("Member", "DELETE", "/api/member/{0}/awards/{1}", null)]
    public async Task AwardOfAnotherMember_UnderTheRouteMember_ShouldReturn404(string role, string method, string routeTemplate, string? jsonBody)
    {
        var routeMemberKey = Guid.NewGuid();
        var award = SubmittedAward(memberKey: Guid.NewGuid());
        await using var host = await AwardTestHost.StartAsync(role, routeMemberKey, award);

        var response = await SendAsync(host.Client, method, string.Format(routeTemplate, routeMemberKey, award.MemberAwardKey), jsonBody);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        host.Awards.Verify(x => x.Update(It.IsAny<MemberAward>(), It.IsAny<CancellationToken>()), Times.Never);
        host.Awards.Verify(x => x.Delete(It.IsAny<MemberAward>(), It.IsAny<CancellationToken>()), Times.Never);
        Assert.Equal(BadgeProgressStatus.Submitted, award.Status);
    }

    [Theory]
    [InlineData("KV.Vykhovnyk", "POST", "/api/member/{0}/awards/{1}/review", "{\"isApproved\":true}")]
    [InlineData("Member", "DELETE", "/api/member/{0}/awards/{1}", null)]
    public async Task AwardOfTheRouteMember_ShouldSucceed(string role, string method, string routeTemplate, string? jsonBody)
    {
        var routeMemberKey = Guid.NewGuid();
        var award = SubmittedAward(memberKey: routeMemberKey);
        await using var host = await AwardTestHost.StartAsync(role, routeMemberKey, award);

        var response = await SendAsync(host.Client, method, string.Format(routeTemplate, routeMemberKey, award.MemberAwardKey), jsonBody);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private static MemberAward SubmittedAward(Guid memberKey) => new()
    {
        MemberAwardKey = Guid.NewGuid(),
        MemberKey = memberKey,
        Level = MemberAwardLevel.First,
        Status = BadgeProgressStatus.Submitted
    };

    private static Task<HttpResponseMessage> SendAsync(HttpClient client, string method, string route, string? jsonBody)
    {
        var request = new HttpRequestMessage(new HttpMethod(method), route);
        if (!string.IsNullOrWhiteSpace(jsonBody))
        {
            request.Content = new StringContent(jsonBody, Encoding.UTF8, "application/json");
        }

        return client.SendAsync(request);
    }

    private sealed class AwardTestHost : IAsyncDisposable
    {
        private readonly WebApplication _app;

        private AwardTestHost(WebApplication app, Mock<IMemberAwardRepository> awards)
        {
            _app = app;
            Awards = awards;
            Client = app.GetTestClient();
        }

        public HttpClient Client { get; }

        public Mock<IMemberAwardRepository> Awards { get; }

        /// <summary>
        /// A caller the resource check lets act on the route member — a впорядник leading his
        /// гурток, or a bare member who is that member — so the real Review/Delete handlers decide.
        /// </summary>
        public static async Task<AwardTestHost> StartAsync(string role, Guid routeMemberKey, MemberAward award)
        {
            var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Testing" });
            builder.WebHost.UseTestServer();
            builder.Services.AddHttpContextAccessor();

            var userId = Guid.NewGuid();
            var kurinKey = Guid.NewGuid();
            var groupKey = Guid.NewGuid();
            var routeMemberUserKey = role == "Member" ? userId : Guid.NewGuid();
            builder.Services.AddSingleton(new AwardAuthState(role, userId, kurinKey));

            builder.Services
                .AddAuthentication(options =>
                {
                    options.DefaultAuthenticateScheme = AwardAuthHandler.SchemeName;
                    options.DefaultChallengeScheme = AwardAuthHandler.SchemeName;
                })
                .AddScheme<AuthenticationSchemeOptions, AwardAuthHandler>(AwardAuthHandler.SchemeName, _ => { });
            builder.Services.AddAuthorization(options => options.AddProjectPolicies());

            var scopeReader = new Mock<IResourceScopeReader>();
            scopeReader
                .Setup(x => x.GetScopeAsync(ResourceType.Member, routeMemberKey, It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ResourceScope(kurinKey, groupKey, routeMemberUserKey));
            scopeReader
                .Setup(x => x.GetLedGroupKeysAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new[] { groupKey });

            var awards = new Mock<IMemberAwardRepository>();
            awards
                .Setup(x => x.GetByKeyAsync(award.MemberAwardKey, It.IsAny<CancellationToken>()))
                .ReturnsAsync(award);
            var unitOfWork = new Mock<IMemberUnitOfWork>();
            unitOfWork.SetupGet(x => x.MemberAwards).Returns(awards.Object);
            unitOfWork.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

            var mapper = new Mock<IMapper>();
            mapper.Setup(x => x.Map<MemberAwardDto>(It.IsAny<MemberAward>())).Returns(new MemberAwardDto());
            var members = new Mock<IMemberDirectory>();
            members.Setup(x => x.FindAccountKeyAsync(routeMemberKey, It.IsAny<CancellationToken>())).ReturnsAsync(routeMemberUserKey);

            builder.Services.AddScoped<IMediator>(services =>
            {
                var currentUser = services.GetRequiredService<ICurrentUserContext>();
                var access = services.GetRequiredService<IResourceAccessService>();
                var review = new ReviewMemberAwardCommandHandler(
                    unitOfWork.Object,
                    members.Object,
                    currentUser,
                    Mock.Of<IDomainEventPublisher>(),
                    mapper.Object,
                    access);
                var delete = new DeleteMemberAwardCommandHandler(unitOfWork.Object, currentUser, members.Object, access);

                var mediator = new Mock<IMediator>();
                mediator
                    .Setup(x => x.Send(It.IsAny<ReviewMemberAwardCommand>(), It.IsAny<CancellationToken>()))
                    .Returns((IRequest<ServiceResult<MemberAwardDto>> command, CancellationToken ct) =>
                        review.Handle((ReviewMemberAwardCommand)command, ct));
                mediator
                    .Setup(x => x.Send(It.IsAny<DeleteMemberAwardCommand>(), It.IsAny<CancellationToken>()))
                    .Returns((IRequest<ServiceResult<Unit>> command, CancellationToken ct) =>
                        delete.Handle((DeleteMemberAwardCommand)command, ct));
                return mediator.Object;
            });

            builder.Services.AddScoped(_ => scopeReader.Object);
            builder.Services.AddSingleton(Mock.Of<IAwardImagesCatalog>());
            builder.Services.AddScoped<ICurrentUserContext, HttpCurrentUserContext>();
            builder.Services.AddScoped<IResourceAccessService, ResourceAccessService>();
            builder.Services.AddScoped<ICurrentPermissions, CurrentPermissions>();
            builder.Services.AddSingleton<IKurinAccessPolicyProvider>(new FixedKurinAccessPolicyProvider());
            builder.Services.AddMemoryCache();
            builder.Services.AddSingleton<IBackendCache, MemoryBackendCache>();
            builder.Services.AddControllers().AddApplicationPart(typeof(MemberAwardsController).Assembly);

            var app = builder.Build();
            app.UseRouting();
            app.UseAuthentication();
            app.UseAuthorization();
            app.MapControllers();

            await app.StartAsync();
            return new AwardTestHost(app, awards);
        }

        public async ValueTask DisposeAsync()
        {
            await _app.StopAsync();
            await _app.DisposeAsync();
        }
    }

    private sealed record AwardAuthState(string Role, Guid UserId, Guid KurinKey);

    private sealed class AwardAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        AwardAuthState authState)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        public const string SchemeName = "MemberAwardRouteMemberTest";

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, authState.UserId.ToString()),
                new("sub", authState.UserId.ToString()),
                new("kurinKey", authState.KurinKey.ToString()),
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
