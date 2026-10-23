using Microsoft.AspNetCore.Identity;
using Moq;
using ProjectK.BusinessLogic.Modules.AuthModule.Features.RefreshToken.Refresh;
using ProjectK.BusinessLogic.Modules.AuthModule.Services;
using ProjectK.BusinessLogic.Tests.TestHelpers;
using ProjectK.Common.Entities.AuthModule;
using ProjectK.Common.Interfaces.Modules.AuthModule;
using ProjectK.Common.Interfaces.Modules.InfrastructureModule;
using ProjectK.Common.Interfaces.Modules.MemberModule;
using ProjectK.Common.Models.Authorization;
using ProjectK.Common.Models.Authorization.KurinPolicy;
using ProjectK.Common.Models.Enums;

namespace ProjectK.BusinessLogic.Tests.AuthModule.HandlerTests.RefreshToken;

public class RefreshTokenCommandHandlerTests
{
    private readonly Mock<UserManager<AppUser>> _userManagerMock;
    private readonly Mock<IJwtService> _jwtServiceMock;
    private readonly Mock<IRefreshTokenStore> _refreshTokensMock;
    private readonly RefreshTokenCommandHandler _handler;

    public RefreshTokenCommandHandlerTests()
    {
        var userStoreMock = new Mock<IUserStore<AppUser>>();
        _userManagerMock = new Mock<UserManager<AppUser>>(
            userStoreMock.Object, null, null, null, null, null, null, null, null);
        _jwtServiceMock = new Mock<IJwtService>();
        _refreshTokensMock = new Mock<IRefreshTokenStore>();
        _handler = HandlerFor(FakeAccessContext.With());
    }

    private RefreshTokenCommandHandler HandlerFor(IAccessContextResolver access) => new(
        _userManagerMock.Object, _jwtServiceMock.Object, _refreshTokensMock.Object,
        access, new AccessSnapshotFactory(new PermissionResolver(new FixedKurinAccessPolicyProvider())));

    [Fact]
    public async Task Handle_ShouldCarryTheAccess_OfAnOfficeAssignedSinceTheLastToken()
    {
        var kurinKey = Guid.NewGuid();
        var user = CreateUser(kurinKey);
        var vykhovnyk = SystemRole.ForOffice(LeadershipType.KV, LeadershipRole.Vykhovnyk);
        var roles = new List<string> { SystemRole.Member };
        var access = new Mock<IAccessContextResolver>();
        access.Setup(r => r.ResolveAsync(user, It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => new AccessContext(user.Id, kurinKey, roles.ToArray()));
        var handler = HandlerFor(access.Object);
        GivenActiveSession("before", user);
        GivenIssuedTokens(user, "access", "rotated");
        var before = await handler.Handle(new RefreshTokenCommand("before"), CancellationToken.None);

        roles.Add(vykhovnyk);
        GivenActiveSession("after", user);
        var after = await handler.Handle(new RefreshTokenCommand("after"), CancellationToken.None);

        Assert.DoesNotContain(vykhovnyk, before.Data!.Access.Roles);
        Assert.Contains(vykhovnyk, after.Data!.Access.Roles);
        Assert.False(after.Data.Access.IsAdmin);
        Assert.Equal(kurinKey.ToString(), after.Data.Access.KurinKey);
        var expected = RolePermissionMap.Resolve(roles, KurinAccessPolicy.Default).Select(p => p.ToClaimValue());
        Assert.Equal(expected.Order(), after.Data.Access.Permissions.Order());
        Assert.NotEqual(before.Data.Access.Permissions.Order(), after.Data.Access.Permissions.Order());
    }

    [Fact]
    public async Task Handle_ShouldAnswerTheSameAccess_AsSigningIn()
    {
        var kurinKey = Guid.NewGuid();
        var user = CreateUser(kurinKey);
        var access = FakeAccessContext.With(SystemRole.ForOffice(LeadershipType.Group, LeadershipRole.Hurtkoviy));
        var snapshots = new AccessSnapshotFactory(new PermissionResolver(new FixedKurinAccessPolicyProvider()));
        var members = new Mock<IMemberDirectory>();
        var login = new LoginResponseFactory(access, _jwtServiceMock.Object, members.Object, _refreshTokensMock.Object, snapshots);
        GivenActiveSession("session", user);
        GivenIssuedTokens(user, "access", "rotated");

        var signedIn = await login.CreateAsync(user, CancellationToken.None);
        var refreshed = await HandlerFor(access).Handle(new RefreshTokenCommand("session"), CancellationToken.None);

        var snapshot = refreshed.Data!.Access;
        Assert.Equal(signedIn.Permissions, snapshot.Permissions);
        Assert.Equal(signedIn.Roles, snapshot.Roles);
        Assert.Equal(signedIn.IsAdmin, snapshot.IsAdmin);
        Assert.Equal(signedIn.KurinKey, snapshot.KurinKey);
    }

    /// <summary>SEC-4.3: a token issued before the suspension must not keep the session alive.</summary>
    [Theory]
    [InlineData(OnboardingStatus.Suspended)]
    [InlineData(OnboardingStatus.Archived)]
    public async Task Handle_ShouldRefuse_WhenTheAccountIsSuspended(OnboardingStatus status)
    {
        var user = CreateUser(kurinKey: Guid.NewGuid());
        user.OnboardingStatus = status;
        var session = GivenActiveSession("issued-before-suspension", user);
        GivenIssuedTokens(user, "access", "rotated");

        var result = await _handler.Handle(new RefreshTokenCommand(session.Token), CancellationToken.None);

        Assert.Equal(ResultType.Unauthorized, result.Type);
        _refreshTokensMock.Verify(
            store => store.IssueAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_ShouldRotateTheSession_WhenTheTokenIsActive()
    {
        var user = CreateUser(kurinKey: Guid.NewGuid());
        var session = GivenActiveSession("valid-refresh-token", user);
        var issued = GivenIssuedTokens(user, "new-access-token", "new-refresh-token");

        var result = await _handler.Handle(new RefreshTokenCommand(session.Token), CancellationToken.None);

        Assert.Equal(ResultType.Success, result.Type);
        Assert.Equal("new-access-token", result.Data!.AccessToken);
        Assert.Equal(issued.Token, result.Data.RefreshToken.Token);

        // The token that was presented is spent, and its replacement takes its place.
        _refreshTokensMock.Verify(store => store.RevokeAsync(session.Token, It.IsAny<CancellationToken>()), Times.Once);
        _refreshTokensMock.Verify(
            store => store.IssueAsync(user.Id, issued.Token, issued.Expires, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_ShouldNotTouchTheAccountsOtherSessions()
    {
        var user = CreateUser(kurinKey: Guid.NewGuid());
        var session = GivenActiveSession("this-browser", user);
        GivenIssuedTokens(user, "access", "rotated");

        await _handler.Handle(new RefreshTokenCommand(session.Token), CancellationToken.None);

        // The whole point of a row per session: refreshing on one device must not sign the same
        // person out on another.
        _refreshTokensMock.Verify(
            store => store.RevokeAllAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_ShouldScopeTheAccessTokenToTheUsersKurin()
    {
        var kurinKey = Guid.NewGuid();
        var user = CreateUser(kurinKey);
        var session = GivenActiveSession("valid-refresh-token", user);
        GivenIssuedTokens(user, "scoped-access", "rotated", roles: ["Admin", "KV.Zvyazkovyi"]);

        await _handler.Handle(new RefreshTokenCommand(session.Token), CancellationToken.None);

        _jwtServiceMock.Verify(
            service => service.GenerateAccessToken(
                user.Id.ToString(), user.Email, It.IsAny<IEnumerable<string>>(), kurinKey.ToString(), It.IsAny<bool>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_ShouldReturnUnauthorized_WhenTheTokenIsNotAnActiveSession()
    {
        // Revoked, expired and never-issued all look the same from here: the store answers null.
        _refreshTokensMock
            .Setup(store => store.FindActiveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserRefreshToken?)null);

        var result = await _handler.Handle(new RefreshTokenCommand("spent-token"), CancellationToken.None);

        Assert.Equal(ResultType.Unauthorized, result.Type);
        Assert.Null(result.Data);
        _refreshTokensMock.Verify(
            store => store.IssueAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _jwtServiceMock.Verify(service => service.GenerateRefreshToken(), Times.Never);
    }

    [Fact]
    public async Task Handle_ShouldReturnUnauthorized_WhenAnotherRefreshAlreadySpentTheToken()
    {
        // Two refreshes race on the same cookie: both find it active, only one revokes it. The
        // loser must not mint a second session that nobody holds and logout cannot reach.
        var user = CreateUser(kurinKey: Guid.NewGuid());
        var session = GivenActiveSession("contested", user);
        GivenIssuedTokens(user, "access", "rotated");
        _refreshTokensMock
            .Setup(store => store.RevokeAsync(session.Token, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var result = await _handler.Handle(new RefreshTokenCommand(session.Token), CancellationToken.None);

        Assert.Equal(ResultType.Unauthorized, result.Type);
        _refreshTokensMock.Verify(
            store => store.IssueAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_ShouldReturnUnauthorized_WhenTheAccountIsGone()
    {
        var session = new UserRefreshToken { UserId = Guid.NewGuid(), Token = "orphan" };
        _refreshTokensMock
            .Setup(store => store.FindActiveAsync(session.Token, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);
        _userManagerMock.Setup(manager => manager.FindByIdAsync(session.UserId.ToString()))
            .ReturnsAsync((AppUser?)null);

        var result = await _handler.Handle(new RefreshTokenCommand(session.Token), CancellationToken.None);

        Assert.Equal(ResultType.Unauthorized, result.Type);
        _refreshTokensMock.Verify(
            store => store.IssueAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    private static AppUser CreateUser(Guid kurinKey) => new()
    {
        Id = Guid.NewGuid(),
        Email = "test@example.com",
        FirstName = "John",
        LastName = "Doe",
        ActiveKurinKey = kurinKey
    };

    private UserRefreshToken GivenActiveSession(string token, AppUser user)
    {
        var session = new UserRefreshToken { UserId = user.Id, Token = token };
        _refreshTokensMock
            .Setup(store => store.FindActiveAsync(token, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);
        _refreshTokensMock
            .Setup(store => store.RevokeAsync(token, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _userManagerMock.Setup(manager => manager.FindByIdAsync(user.Id.ToString())).ReturnsAsync(user);
        return session;
    }

    private Common.Models.Dtos.AuthModule.RefreshToken GivenIssuedTokens(
        AppUser user,
        string accessToken,
        string refreshToken,
        List<string>? roles = null)
    {
        var issued = new Common.Models.Dtos.AuthModule.RefreshToken
        {
            Token = refreshToken,
            Expires = DateTime.UtcNow.AddDays(7),
            Created = DateTime.UtcNow
        };

        _userManagerMock.Setup(manager => manager.GetRolesAsync(user)).ReturnsAsync(roles ?? ["User"]);
        _jwtServiceMock
            .Setup(service => service.GenerateAccessToken(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IEnumerable<string>>(), It.IsAny<string>(), It.IsAny<bool>()))
            .Returns(accessToken);
        _jwtServiceMock.Setup(service => service.GenerateRefreshToken()).Returns(issued);

        return issued;
    }
}
