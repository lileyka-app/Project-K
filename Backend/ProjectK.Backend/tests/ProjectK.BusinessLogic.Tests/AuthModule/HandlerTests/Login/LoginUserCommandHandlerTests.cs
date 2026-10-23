using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using ProjectK.Common.Models.Settings;
using Microsoft.Extensions.Hosting;
using Moq;
using ProjectK.BusinessLogic.Modules.AuthModule.Features.User.Login;
using ProjectK.BusinessLogic.Modules.AuthModule.Services;
using ProjectK.BusinessLogic.Tests.TestHelpers;
using ProjectK.Common.Entities.AuthModule;
using ProjectK.Common.Entities.KurinModule;
using ProjectK.Common.Extensions;
using ProjectK.Common.Interfaces;
using ProjectK.Common.Interfaces.Modules.AuthModule;
using ProjectK.Common.Interfaces.Modules.InfrastructureModule;
using ProjectK.Common.Interfaces.Modules.KurinModule;
using ProjectK.Common.Interfaces.Modules.MemberModule;
using ProjectK.Common.Models.Authorization;
using ProjectK.Common.Models.Dtos.AuthModule;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Records;

namespace ProjectK.BusinessLogic.Tests.AuthModule.HandlerTests.Login;

public class LoginUserCommandHandlerTests
{
    private readonly Mock<UserManager<AppUser>> _userManagerMock;
    private readonly Mock<IRefreshTokenStore> _refreshTokensMock;
    private readonly Mock<IAccessContextResolver> _accessMock = FakeAccessContext.Resolver();
    private readonly Mock<SignInManager<AppUser>> _signInManagerMock;
    private readonly Mock<IJwtService> _jwtServiceMock;
    private readonly Mock<IUnitOfWork> _uowMock;
    private Mock<IMemberDirectory> _memberDirectoryMock;
    private readonly ILoginResponseFactory _loginResponseFactory;
    private readonly Mock<IActivityLogger> _activityLoggerMock;
    private readonly Mock<IConfiguration> _configurationMock;
    private const int ThrottleLimit = 3;
    private readonly ILoginThrottle _throttle = new LoginThrottle(
        new MemoryCache(new MemoryCacheOptions()),
        Options.Create(new LoginThrottleOptions { FailureLimit = ThrottleLimit }));

    private readonly LoginUserCommandHandler _handler;

    public LoginUserCommandHandlerTests()
    {
        var userStoreMock = new Mock<IUserStore<AppUser>>();
        _userManagerMock = new Mock<UserManager<AppUser>>(
            userStoreMock.Object, null, null, null, null, null, null, null, null);

        var contextAccessorMock = new Mock<Microsoft.AspNetCore.Http.IHttpContextAccessor>();
        var userPrincipalFactoryMock = new Mock<IUserClaimsPrincipalFactory<AppUser>>();
        _signInManagerMock = new Mock<SignInManager<AppUser>>(
            _userManagerMock.Object, contextAccessorMock.Object, userPrincipalFactoryMock.Object, null, null, null, null);

        _jwtServiceMock = new Mock<IJwtService>();
        _activityLoggerMock = new Mock<IActivityLogger>();
        _configurationMock = new Mock<IConfiguration>();
        var sectionMock = new Mock<IConfigurationSection>();
        sectionMock.Setup(s => s.Value).Returns((string?)null);
        _configurationMock.Setup(c => c.GetSection(It.IsAny<string>())).Returns(sectionMock.Object);

        _memberDirectoryMock = new Mock<IMemberDirectory>();
        _memberDirectoryMock.Setup(r => r.FindByAccountAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((MemberSummary?)null);
        _uowMock = new Mock<IUnitOfWork>();

        _refreshTokensMock = new Mock<IRefreshTokenStore>();
        _loginResponseFactory = new LoginResponseFactory(_accessMock.Object, _jwtServiceMock.Object, _memberDirectoryMock.Object, _refreshTokensMock.Object, new PermissionResolver(new FixedKurinAccessPolicyProvider()));
        _handler = CreateHandler(_configurationMock.Object, Environments.Development);
    }

    private LoginUserCommandHandler CreateHandler(IConfiguration configuration, string environmentName)
    {
        var environment = new Mock<IHostEnvironment>();
        environment.SetupGet(e => e.EnvironmentName).Returns(environmentName);

        return new LoginUserCommandHandler(
            _userManagerMock.Object,
            _signInManagerMock.Object,
            _loginResponseFactory,
            _activityLoggerMock.Object,
            configuration,
            _jwtServiceMock.Object,
            environment.Object,
            _throttle);
    }

    private static IConfiguration BypassConfiguration() =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["E2E:BypassPrivilegedMfa"] = "true" })
            .Build();

    [Theory]
    [InlineData("Production")]
    [InlineData("Staging")]
    public async Task Handle_ShouldStillRequireMfa_WhenBypassIsSetOnADeployedTier(string environmentName)
    {
        // The e2e switch is a test-tier convenience. Read without a guard, it turned the second
        // factor off for every account of a production instance that carried the key.
        var user = new AppUser
        {
            Id = Guid.NewGuid(),
            Email = "2fa@example.com",
            TwoFactorEnabled = true,
            FirstName = "TwoFactor",
            LastName = "User"
        };

        _userManagerMock.Setup(x => x.FindByEmailAsync(user.Email)).ReturnsAsync(user);
        _signInManagerMock.Setup(x => x.CheckPasswordSignInAsync(user, "password123", false))
            .ReturnsAsync(Microsoft.AspNetCore.Identity.SignInResult.Success);
        _jwtServiceMock.Setup(x => x.GenerateMfaChallengeToken(user.Id)).Returns("challenge");

        var handler = CreateHandler(BypassConfiguration(), environmentName);

        var result = await handler.Handle(new LoginUserCommand(user.Email, "password123"), CancellationToken.None);

        Assert.Equal(ResultType.Success, result.Type);
        Assert.True(result.Data!.RequiresMfa);
        Assert.Null(result.Data.Tokens);
        Assert.Equal("challenge", result.Data.MfaToken);
    }

    [Fact]
    public async Task Handle_ShouldHonourBypass_OnATestTier()
    {
        var user = new AppUser
        {
            Id = Guid.NewGuid(),
            Email = "2fa@example.com",
            TwoFactorEnabled = true,
            FirstName = "TwoFactor",
            LastName = "User"
        };

        _userManagerMock.Setup(x => x.FindByEmailAsync(user.Email)).ReturnsAsync(user);
        _signInManagerMock.Setup(x => x.CheckPasswordSignInAsync(user, "password123", false))
            .ReturnsAsync(Microsoft.AspNetCore.Identity.SignInResult.Success);
        _userManagerMock.Setup(x => x.GetRolesAsync(user)).ReturnsAsync(new List<string>());
        _jwtServiceMock.Setup(x => x.GenerateAccessToken(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IEnumerable<string>>(), It.IsAny<string?>(), It.IsAny<bool>()))
            .Returns("access");
        _jwtServiceMock.Setup(x => x.GenerateRefreshToken())
            .Returns(new ProjectK.Common.Models.Dtos.AuthModule.RefreshToken { Token = "refresh", Expires = DateTime.UtcNow.AddDays(7) });

        var handler = CreateHandler(BypassConfiguration(), "E2E");

        var result = await handler.Handle(new LoginUserCommand(user.Email, "password123"), CancellationToken.None);

        Assert.Equal(ResultType.Success, result.Type);
        Assert.False(result.Data!.RequiresMfa);
        Assert.NotNull(result.Data.Tokens);
    }

    [Fact]
    public async Task Handle_ShouldReturnSuccess_WhenValidCredentials()
    {
        // Arrange
        var email = "test@example.com";
        var password = "password123";
        var command = new LoginUserCommand(email, password);
        var userId = Guid.NewGuid();
        var kurinKey = Guid.NewGuid();
        var memberKey = Guid.NewGuid();

        var user = new AppUser
        {
            Id = userId,
            Email = email,
            FirstName = "John",
            LastName = "Doe",
            ActiveKurinKey = kurinKey
        };

        var roles = new List<string> { "User" };
        var accessToken = "access-token";
        var refreshToken = new ProjectK.Common.Models.Dtos.AuthModule.RefreshToken
        {
            Token = "refresh-token",
            Expires = DateTime.UtcNow.AddDays(7),
            Created = DateTime.UtcNow
        };

        _userManagerMock.Setup(x => x.FindByEmailAsync(email))
            .ReturnsAsync(user);
        _signInManagerMock.Setup(x => x.CheckPasswordSignInAsync(user, password, false))
            .ReturnsAsync(Microsoft.AspNetCore.Identity.SignInResult.Success);
        _accessMock.Setup(x => x.ResolveAsync(user, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AccessContext(user.Id, user.ResolveScopeKurinKey(), roles));
        _userManagerMock.Setup(x => x.UpdateAsync(user))
            .ReturnsAsync(IdentityResult.Success);
        _jwtServiceMock.Setup(x => x.GenerateAccessToken(userId.ToString(), email, roles, kurinKey.ToString(), It.IsAny<bool>()))
            .Returns(accessToken);
        _jwtServiceMock.Setup(x => x.GenerateRefreshToken())
            .Returns(refreshToken);

        _memberDirectoryMock.Setup(x => x.FindByAccountAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MemberSummary(memberKey, userId, Guid.Empty, null, "A", "B", "a@example.com", null));

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.Equal(ResultType.Success, result.Type);
        Assert.NotNull(result.Data);
        Assert.Equal(userId, result.Data.UserKey);
        Assert.Equal(memberKey, result.Data.MemberKey);
        Assert.Equal(email, result.Data.Email);
        Assert.False(result.Data.IsAdmin);
        Assert.Equal(kurinKey.ToString(), result.Data.KurinKey);
        Assert.Equal(accessToken, result.Data.Tokens.AccessToken);
        Assert.Equal(refreshToken.Token, result.Data.Tokens.RefreshToken.Token);

        // Verify user was updated with new refresh token
        // Recorded as a new session rather than replacing whatever the account already had.
        _refreshTokensMock.Verify(
            store => store.IssueAsync(user.Id, refreshToken.Token, refreshToken.Expires, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_ShouldReturnSuccess_WhenValidCredentialsAndEmptyKurinKey()
    {
        // Arrange
        var email = "admin@example.com";
        var password = "adminpassword";
        var command = new LoginUserCommand(email, password);
        var userId = Guid.NewGuid();

        var user = new AppUser
        {
            Id = userId,
            Email = email,
            FirstName = "Admin",
            LastName = "User"
        };

        var roles = new List<string> { "Admin" };
        var accessToken = "admin-access-token";
        var refreshToken = new Common.Models.Dtos.AuthModule.RefreshToken
        {
            Token = "admin-refresh-token",
            Expires = DateTime.UtcNow.AddDays(7),
            Created = DateTime.UtcNow
        };

        _userManagerMock.Setup(x => x.FindByEmailAsync(email))
            .ReturnsAsync(user);
        _signInManagerMock.Setup(x => x.CheckPasswordSignInAsync(user, password, false))
            .ReturnsAsync(Microsoft.AspNetCore.Identity.SignInResult.Success);
        _accessMock.Setup(x => x.ResolveAsync(user, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AccessContext(user.Id, user.ResolveScopeKurinKey(), roles));
        _userManagerMock.Setup(x => x.UpdateAsync(user))
            .ReturnsAsync(IdentityResult.Success);
        _jwtServiceMock.Setup(x => x.GenerateAccessToken(userId.ToString(), email, roles, null, It.IsAny<bool>()))
            .Returns(accessToken);
        _jwtServiceMock.Setup(x => x.GenerateRefreshToken())
            .Returns(refreshToken);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.Equal(ResultType.Success, result.Type);
        Assert.NotNull(result.Data);
        Assert.Equal(userId, result.Data.UserKey);
        Assert.Equal(email, result.Data.Email);
        Assert.True(result.Data.IsAdmin);
        Assert.Null(result.Data.KurinKey);

        _jwtServiceMock.Verify(x => x.GenerateAccessToken(userId.ToString(), email, roles, null, It.IsAny<bool>()), Times.Once);
    }

    /// <summary>
    /// SEC-4.3: a suspended account answers exactly like a wrong password, so that nobody can
    /// tell a suspended address from a mistyped one.
    /// </summary>
    [Theory]
    [InlineData(OnboardingStatus.Suspended)]
    [InlineData(OnboardingStatus.Archived)]
    public async Task Handle_ShouldRefuseASuspendedAccount_WithTheSameAnswerAsAWrongPassword(OnboardingStatus status)
    {
        var user = new AppUser
        {
            Id = Guid.NewGuid(),
            Email = "suspended@example.com",
            FirstName = "Sus",
            LastName = "Pended",
            OnboardingStatus = status
        };
        _userManagerMock.Setup(x => x.FindByEmailAsync(user.Email)).ReturnsAsync(user);
        _signInManagerMock.Setup(x => x.CheckPasswordSignInAsync(user, "password123", false))
            .ReturnsAsync(Microsoft.AspNetCore.Identity.SignInResult.Success);

        var result = await _handler.Handle(new LoginUserCommand(user.Email, "password123"), CancellationToken.None);

        Assert.Equal(ResultType.Unauthorized, result.Type);
        Assert.Equal("InvalidCredentials", result.ErrorCode);
        Assert.Null(result.Data);
        _jwtServiceMock.Verify(x => x.GenerateRefreshToken(), Times.Never);
        _activityLoggerMock.Verify(x => x.TrackFailedLogin(user.Email), Times.Once);
    }

    [Fact]
    public async Task Handle_ShouldReturnUnauthorized_WhenUserNotFound()
    {
        // Arrange
        var email = "nonexistent@example.com";
        var password = "password123";
        var command = new LoginUserCommand(email, password);

        _userManagerMock.Setup(x => x.FindByEmailAsync(email))
            .ReturnsAsync((AppUser?)null);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.Equal(ResultType.Unauthorized, result.Type);
        Assert.Null(result.Data);

        _userManagerMock.Verify(x => x.FindByEmailAsync(email), Times.Once);
        _signInManagerMock.Verify(x => x.CheckPasswordSignInAsync(It.IsAny<AppUser>(), It.IsAny<string>(), It.IsAny<bool>()), Times.Never);
        _userManagerMock.Verify(x => x.GetRolesAsync(It.IsAny<AppUser>()), Times.Never);
        _userManagerMock.Verify(x => x.UpdateAsync(It.IsAny<AppUser>()), Times.Never);
        _jwtServiceMock.Verify(x => x.GenerateAccessToken(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IEnumerable<string>>(), It.IsAny<string>(), It.IsAny<bool>()), Times.Never);
        _jwtServiceMock.Verify(x => x.GenerateRefreshToken(), Times.Never);
    }

    [Fact]
    public async Task Handle_ShouldReturnUnauthorized_WhenPasswordIsIncorrect()
    {
        // Arrange
        var email = "test@example.com";
        var password = "wrongpassword";
        var command = new LoginUserCommand(email, password);
        var userId = Guid.NewGuid();

        var user = new AppUser
        {
            Id = userId,
            Email = email,
            FirstName = "John",
            LastName = "Doe"
        };

        _userManagerMock.Setup(x => x.FindByEmailAsync(email))
            .ReturnsAsync(user);
        _signInManagerMock.Setup(x => x.CheckPasswordSignInAsync(user, password, false))
            .ReturnsAsync(Microsoft.AspNetCore.Identity.SignInResult.Failed);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.Equal(ResultType.Unauthorized, result.Type);
        Assert.Null(result.Data);

        _userManagerMock.Verify(x => x.FindByEmailAsync(email), Times.Once);
        _signInManagerMock.Verify(x => x.CheckPasswordSignInAsync(user, password, false), Times.Once);
        _userManagerMock.Verify(x => x.GetRolesAsync(It.IsAny<AppUser>()), Times.Never);
        _userManagerMock.Verify(x => x.UpdateAsync(It.IsAny<AppUser>()), Times.Never);
        _jwtServiceMock.Verify(x => x.GenerateAccessToken(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IEnumerable<string>>(), It.IsAny<string>(), It.IsAny<bool>()), Times.Never);
        _jwtServiceMock.Verify(x => x.GenerateRefreshToken(), Times.Never);
    }

    [Fact]
    public async Task Handle_ShouldReturnUnauthorized_WhenAccountIsLockedOut()
    {
        // Arrange
        var email = "lockedout@example.com";
        var password = "password123";
        var command = new LoginUserCommand(email, password);
        var userId = Guid.NewGuid();

        var user = new AppUser
        {
            Id = userId,
            Email = email,
            FirstName = "Locked",
            LastName = "User"
        };

        _userManagerMock.Setup(x => x.FindByEmailAsync(email))
            .ReturnsAsync(user);
        _signInManagerMock.Setup(x => x.CheckPasswordSignInAsync(user, password, false))
            .ReturnsAsync(Microsoft.AspNetCore.Identity.SignInResult.LockedOut);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.Equal(ResultType.Unauthorized, result.Type);
        Assert.Null(result.Data);

        _signInManagerMock.Verify(x => x.CheckPasswordSignInAsync(user, password, false), Times.Once);
    }

    [Fact]
    public async Task Handle_ShouldReturnRequiresMfa_WhenTwoFactorRequired()
    {
        // Arrange
        var email = "2fa@example.com";
        var password = "password123";
        var command = new LoginUserCommand(email, password);
        var userId = Guid.NewGuid();

        var user = new AppUser
        {
            Id = userId,
            Email = email,
            TwoFactorEnabled = true,
            FirstName = "TwoFactor",
            LastName = "User"
        };

        _userManagerMock.Setup(x => x.FindByEmailAsync(email))
            .ReturnsAsync(user);
        _signInManagerMock.Setup(x => x.CheckPasswordSignInAsync(user, password, false))
            .ReturnsAsync(Microsoft.AspNetCore.Identity.SignInResult.Success);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.Equal(ResultType.Success, result.Type);
        Assert.NotNull(result.Data);
        Assert.Equal(userId, result.Data.UserKey);
        Assert.Equal(email, result.Data.Email);
        Assert.True(result.Data.RequiresMfa);
        Assert.Null(result.Data.Tokens);

        _signInManagerMock.Verify(x => x.CheckPasswordSignInAsync(user, password, false), Times.Once);
        _userManagerMock.Verify(x => x.GetRolesAsync(It.IsAny<AppUser>()), Times.Never);
        _userManagerMock.Verify(x => x.UpdateAsync(It.IsAny<AppUser>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ShouldHandleMultipleRoles()
    {
        // Arrange
        var email = "multirole@example.com";
        var password = "password123";
        var command = new LoginUserCommand(email, password);
        var userId = Guid.NewGuid();
        var kurinKey = Guid.NewGuid();

        var user = new AppUser
        {
            Id = userId,
            Email = email,
            FirstName = "Multi",
            LastName = "Role",
            ActiveKurinKey = kurinKey
        };

        var roles = new List<string> { "Admin", "Manager", "User" };
        var accessToken = "multi-role-token";
        var refreshToken = new Common.Models.Dtos.AuthModule.RefreshToken
        {
            Token = "multi-role-refresh",
            Expires = DateTime.UtcNow.AddDays(7),
            Created = DateTime.UtcNow
        };

        _userManagerMock.Setup(x => x.FindByEmailAsync(email))
            .ReturnsAsync(user);
        _signInManagerMock.Setup(x => x.CheckPasswordSignInAsync(user, password, false))
            .ReturnsAsync(Microsoft.AspNetCore.Identity.SignInResult.Success);
        _accessMock.Setup(x => x.ResolveAsync(user, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AccessContext(user.Id, user.ResolveScopeKurinKey(), roles));
        _userManagerMock.Setup(x => x.UpdateAsync(user))
            .ReturnsAsync(IdentityResult.Success);
        _jwtServiceMock.Setup(x => x.GenerateAccessToken(userId.ToString(), email, roles, kurinKey.ToString(), It.IsAny<bool>()))
            .Returns(accessToken);
        _jwtServiceMock.Setup(x => x.GenerateRefreshToken())
            .Returns(refreshToken);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.Equal(ResultType.Success, result.Type);
        Assert.NotNull(result.Data);
        Assert.True(result.Data.IsAdmin);
        Assert.Equal(accessToken, result.Data.Tokens.AccessToken);

        _jwtServiceMock.Verify(x => x.GenerateAccessToken(userId.ToString(), email, roles, kurinKey.ToString(), It.IsAny<bool>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ShouldRecordANewSession()
    {
        // Arrange
        var email = "update@example.com";
        var password = "password123";
        var command = new LoginUserCommand(email, password);
        var userId = Guid.NewGuid();

        var user = new AppUser
        {
            Id = userId,
            Email = email,
            FirstName = "Update",
            LastName = "Test",
        };

        var roles = new List<string> { "User" };
        var newRefreshToken = new Common.Models.Dtos.AuthModule.RefreshToken
        {
            Token = "new-refresh-token",
            Expires = DateTime.UtcNow.AddDays(14),
            Created = DateTime.UtcNow
        };

        _userManagerMock.Setup(x => x.FindByEmailAsync(email))
            .ReturnsAsync(user);
        _signInManagerMock.Setup(x => x.CheckPasswordSignInAsync(user, password, false))
            .ReturnsAsync(Microsoft.AspNetCore.Identity.SignInResult.Success);
        _accessMock.Setup(x => x.ResolveAsync(user, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AccessContext(user.Id, user.ResolveScopeKurinKey(), roles));
        _userManagerMock.Setup(x => x.UpdateAsync(user))
            .ReturnsAsync(IdentityResult.Success);
        _jwtServiceMock.Setup(x => x.GenerateAccessToken(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IEnumerable<string>>(), It.IsAny<string>(), It.IsAny<bool>()))
            .Returns("access-token");
        _jwtServiceMock.Setup(x => x.GenerateRefreshToken())
            .Returns(newRefreshToken);

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        // Recorded as a new session. Nothing the account already held is touched — signing in here
        // must not sign the same person out on another device.
        _refreshTokensMock.Verify(
            store => store.IssueAsync(userId, newRefreshToken.Token, newRefreshToken.Expires, It.IsAny<CancellationToken>()),
            Times.Once);
        _refreshTokensMock.Verify(
            store => store.RevokeAllAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    /// <summary>
    /// A device that finished the second factor keeps a trust token; while it is valid and the
    /// account's stamp has not moved, the password alone signs in. Signing out and back in on the
    /// same laptop used to mean the authenticator every time.
    /// </summary>
    [Fact]
    public async Task Handle_ShouldSkipMfa_OnATrustedDevice()
    {
        var user = TwoFactorUser();
        user.SecurityStamp = "stamp-1";
        _jwtServiceMock.Setup(x => x.ReadMfaTrust("trusted")).Returns(new MfaTrust(user.Id, "stamp-1"));

        var result = await _handler.Handle(new LoginUserCommand(user.Email!, "password123") { MfaTrustToken = "trusted" }, CancellationToken.None);

        Assert.Equal(ResultType.Success, result.Type);
        Assert.False(result.Data!.RequiresMfa);
        Assert.NotNull(result.Data.Tokens);
    }

    [Theory]
    [InlineData("stamp-old", false)]
    [InlineData("stamp-1", true)]
    public async Task Handle_ShouldAskForTheCodeAgain_WhenTheTrustIsStaleOrSomebodyElses(string stamp, bool otherAccount)
    {
        var user = TwoFactorUser();
        user.SecurityStamp = "stamp-1";
        _jwtServiceMock.Setup(x => x.GenerateMfaChallengeToken(user.Id)).Returns("challenge");
        _jwtServiceMock
            .Setup(x => x.ReadMfaTrust("trusted"))
            .Returns(new MfaTrust(otherAccount ? Guid.NewGuid() : user.Id, stamp));

        var result = await _handler.Handle(new LoginUserCommand(user.Email!, "password123") { MfaTrustToken = "trusted" }, CancellationToken.None);

        Assert.Equal(ResultType.Success, result.Type);
        Assert.True(result.Data!.RequiresMfa);
        Assert.Null(result.Data.Tokens);
    }

    private AppUser TwoFactorUser()
    {
        var user = new AppUser
        {
            Id = Guid.NewGuid(),
            Email = "2fa@example.com",
            TwoFactorEnabled = true,
            FirstName = "TwoFactor",
            LastName = "User"
        };

        _userManagerMock.Setup(x => x.FindByEmailAsync(user.Email)).ReturnsAsync(user);
        _signInManagerMock.Setup(x => x.CheckPasswordSignInAsync(user, "password123", false))
            .ReturnsAsync(Microsoft.AspNetCore.Identity.SignInResult.Success);
        _userManagerMock.Setup(x => x.GetRolesAsync(user)).ReturnsAsync(new List<string>());
        _jwtServiceMock.Setup(x => x.GenerateAccessToken(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IEnumerable<string>>(), It.IsAny<string?>(), It.IsAny<bool>()))
            .Returns("access");
        _jwtServiceMock.Setup(x => x.GenerateRefreshToken())
            .Returns(new ProjectK.Common.Models.Dtos.AuthModule.RefreshToken { Token = "refresh", Expires = DateTime.UtcNow.AddDays(7) });
        return user;
    }

    [Fact]
    public async Task Handle_ShouldPauseAnAddress_AfterTheFailureLimit_EvenWithTheRightPassword()
    {
        var user = TwoFactorUser();
        _signInManagerMock.Setup(x => x.CheckPasswordSignInAsync(user, "wrong", false))
            .ReturnsAsync(Microsoft.AspNetCore.Identity.SignInResult.Failed);

        for (var i = 0; i < ThrottleLimit; i++)
        {
            await _handler.Handle(new LoginUserCommand(user.Email!, "wrong"), CancellationToken.None);
        }

        var result = await _handler.Handle(new LoginUserCommand(user.Email!, "password123"), CancellationToken.None);

        Assert.Equal(ResultType.TooManyRequests, result.Type);
        Assert.Equal("TooManyAttempts", result.ErrorCode);
        _signInManagerMock.Verify(x => x.CheckPasswordSignInAsync(user, "password123", false), Times.Never);
    }

    [Fact]
    public async Task Handle_ShouldPauseAnUnknownAddress_TheSameWay()
    {
        // A pause only for registered addresses would tell a caller which ones exist.
        const string email = "nobody@example.com";
        _userManagerMock.Setup(x => x.FindByEmailAsync(It.IsAny<string>())).ReturnsAsync((AppUser?)null);

        for (var i = 0; i < ThrottleLimit; i++)
        {
            await _handler.Handle(new LoginUserCommand(email, "wrong"), CancellationToken.None);
        }

        var result = await _handler.Handle(new LoginUserCommand(" NOBODY@example.com ", "wrong"), CancellationToken.None);

        Assert.Equal(ResultType.TooManyRequests, result.Type);
        Assert.Equal("TooManyAttempts", result.ErrorCode);
    }

    [Fact]
    public async Task Handle_ShouldForgetFailures_AfterASuccessfulPassword()
    {
        var user = TwoFactorUser();
        _signInManagerMock.Setup(x => x.CheckPasswordSignInAsync(user, "wrong", false))
            .ReturnsAsync(Microsoft.AspNetCore.Identity.SignInResult.Failed);

        for (var i = 0; i < ThrottleLimit - 1; i++)
        {
            await _handler.Handle(new LoginUserCommand(user.Email!, "wrong"), CancellationToken.None);
        }
        await _handler.Handle(new LoginUserCommand(user.Email!, "password123"), CancellationToken.None);
        await _handler.Handle(new LoginUserCommand(user.Email!, "wrong"), CancellationToken.None);

        var result = await _handler.Handle(new LoginUserCommand(user.Email!, "password123"), CancellationToken.None);

        Assert.Equal(ResultType.Success, result.Type);
    }
}
