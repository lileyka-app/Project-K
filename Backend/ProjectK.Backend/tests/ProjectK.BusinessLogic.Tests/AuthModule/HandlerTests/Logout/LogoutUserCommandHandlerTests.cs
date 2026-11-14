using Microsoft.AspNetCore.Identity;
using Moq;
using ProjectK.BusinessLogic.Modules.AuthModule.Features.Session;
using ProjectK.Common.Entities.AuthModule;
using ProjectK.Common.Interfaces.Modules.AuthModule;
using ProjectK.Common.Models.Enums;

namespace ProjectK.BusinessLogic.Tests.AuthModule.HandlerTests.Logout;

public class LogoutUserCommandHandlerTests
{
    private readonly Mock<UserManager<AppUser>> _userManagerMock;
    private readonly Mock<IRefreshTokenStore> _refreshTokensMock;
    private readonly LogoutUserCommandHandler _handler;

    public LogoutUserCommandHandlerTests()
    {
        var userStoreMock = new Mock<IUserStore<AppUser>>();
        _userManagerMock = new Mock<UserManager<AppUser>>(
            userStoreMock.Object, null, null, null, null, null, null, null, null);
        _refreshTokensMock = new Mock<IRefreshTokenStore>();
        _handler = new LogoutUserCommandHandler(_userManagerMock.Object, _refreshTokensMock.Object);
    }

    [Fact]
    public async Task Handle_ShouldRevokeEveryCookieTheBrowserSent()
    {
        // A browser can carry more than one refreshToken cookie — a stale one shadowing the live
        // one. Revoking only the first left the session actually in use valid on the server while
        // telling the person they had signed out.
        var userKey = Guid.NewGuid().ToString();
        var user = CreateUser(userKey);
        _userManagerMock.Setup(x => x.FindByIdAsync(userKey)).ReturnsAsync(user);
        _userManagerMock.Setup(x => x.UpdateAsync(user)).ReturnsAsync(IdentityResult.Success);

        var result = await _handler.Handle(
            new LogoutUserCommand(userKey, ["stale-cookie", "live-cookie"]), CancellationToken.None);

        Assert.Equal(ResultType.Success, result.Type);
        _refreshTokensMock.Verify(store => store.RevokeAsync("stale-cookie", It.IsAny<CancellationToken>()), Times.Once);
        _refreshTokensMock.Verify(store => store.RevokeAsync("live-cookie", It.IsAny<CancellationToken>()), Times.Once);
        _refreshTokensMock.Verify(
            store => store.RevokeAllAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_ShouldEndEverySession_WhenNothingNamesTheOneToEnd()
    {
        // Nothing identifies the session, so it cannot be ended precisely. Ending them all is the
        // safe way to be wrong; answering "logged out" while leaving the session usable is not.
        var userKey = Guid.NewGuid().ToString();
        var user = CreateUser(userKey);
        _userManagerMock.Setup(x => x.FindByIdAsync(userKey)).ReturnsAsync(user);
        _userManagerMock.Setup(x => x.UpdateAsync(user)).ReturnsAsync(IdentityResult.Success);

        var result = await _handler.Handle(new LogoutUserCommand(userKey), CancellationToken.None);

        Assert.Equal(ResultType.Success, result.Type);
        _refreshTokensMock.Verify(
            store => store.RevokeAllAsync(user.Id, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    private static AppUser CreateUser(string userKey) => new()
    {
        Id = Guid.Parse(userKey),
        Email = "test@example.com",
        FirstName = "John",
        LastName = "Doe"
    };

    [Fact]
    public async Task Handle_ShouldReturnSuccess_WhenValidUserKey()
    {
        // Arrange
        var userKey = Guid.NewGuid().ToString();
        var command = new LogoutUserCommand(userKey, ["this-browser"]);
        var user = new AppUser
        {
            Id = Guid.Parse(userKey),
            Email = "test@example.com",
            FirstName = "John",
            LastName = "Doe",
        };

        _userManagerMock.Setup(x => x.FindByIdAsync(userKey))
            .ReturnsAsync(user);
        _userManagerMock.Setup(x => x.UpdateAsync(user))
            .ReturnsAsync(IdentityResult.Success);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.Equal(ResultType.Success, result.Type);
        Assert.Equal("User logged out successfully.", result.Data);
        // Signing out ends this session only — the account may be signed in elsewhere.
        _refreshTokensMock.Verify(
            store => store.RevokeAllAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);

        _userManagerMock.Verify(x => x.FindByIdAsync(userKey), Times.Once);
        _userManagerMock.Verify(x => x.UpdateAsync(user), Times.Once);
    }

    [Fact]
    public async Task Handle_ShouldClearActiveKurinScope()
    {
        var userKey = Guid.NewGuid().ToString();
        var user = new AppUser
        {
            Id = Guid.Parse(userKey),
            Email = "admin@projectk.com",
            FirstName = "System",
            LastName = "Admin",
            ActiveKurinKey = Guid.NewGuid()
        };

        _userManagerMock.Setup(x => x.FindByIdAsync(userKey)).ReturnsAsync(user);
        _userManagerMock.Setup(x => x.UpdateAsync(user)).ReturnsAsync(IdentityResult.Success);

        var result = await _handler.Handle(new LogoutUserCommand(userKey, ["this-browser"]), CancellationToken.None);

        Assert.Equal(ResultType.Success, result.Type);
        Assert.Null(user.ActiveKurinKey);
    }

    [Fact]
    public async Task Handle_ShouldReturnUnauthorized_WhenUserKeyIsNull()
    {
        // Arrange
        var command = new LogoutUserCommand(null!);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.Equal(ResultType.Unauthorized, result.Type);
        Assert.Equal("Access token is missing or invalid.", result.ErrorMessage);

        _userManagerMock.Verify(x => x.FindByIdAsync(It.IsAny<string>()), Times.Never);
        _userManagerMock.Verify(x => x.UpdateAsync(It.IsAny<AppUser>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ShouldReturnUnauthorized_WhenUserKeyIsEmpty()
    {
        // Arrange
        var command = new LogoutUserCommand(string.Empty);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.Equal(ResultType.Unauthorized, result.Type);
        Assert.Equal("Access token is missing or invalid.", result.ErrorMessage);

        _userManagerMock.Verify(x => x.FindByIdAsync(It.IsAny<string>()), Times.Never);
        _userManagerMock.Verify(x => x.UpdateAsync(It.IsAny<AppUser>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ShouldReturnNotFound_WhenUserNotFound()
    {
        // Arrange
        var userKey = Guid.NewGuid().ToString();
        var command = new LogoutUserCommand(userKey, ["this-browser"]);

        _userManagerMock.Setup(x => x.FindByIdAsync(userKey))
            .ReturnsAsync((AppUser?)null);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.Equal(ResultType.NotFound, result.Type);
        Assert.Equal("User not found.", result.ErrorMessage);

        _userManagerMock.Verify(x => x.FindByIdAsync(userKey), Times.Once);
        _userManagerMock.Verify(x => x.UpdateAsync(It.IsAny<AppUser>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ShouldEndOnlyTheSessionItWasGiven()
    {
        // Arrange
        var userKey = Guid.NewGuid().ToString();
        var command = new LogoutUserCommand(userKey, ["this-browser"]);
        var user = new AppUser
        {
            Id = Guid.Parse(userKey),
            Email = "active@example.com",
            FirstName = "Active",
            LastName = "User",
        };

        _userManagerMock.Setup(x => x.FindByIdAsync(userKey))
            .ReturnsAsync(user);
        _userManagerMock.Setup(x => x.UpdateAsync(user))
            .ReturnsAsync(IdentityResult.Success);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.Equal(ResultType.Success, result.Type);
        // Signing out ends this session only — the account may be signed in elsewhere.
        _refreshTokensMock.Verify(
            store => store.RevokeAllAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _userManagerMock.Verify(x => x.UpdateAsync(user), Times.Once);
    }

    [Fact]
    public async Task Handle_ShouldSucceed_WhenUserAlreadyHasNullRefreshToken()
    {
        // Arrange
        var userKey = Guid.NewGuid().ToString();
        var command = new LogoutUserCommand(userKey, ["this-browser"]);
        var user = new AppUser
        {
            Id = Guid.Parse(userKey),
            Email = "already@example.com",
            FirstName = "Already",
            LastName = "LoggedOut"
        };

        _userManagerMock.Setup(x => x.FindByIdAsync(userKey))
            .ReturnsAsync(user);
        _userManagerMock.Setup(x => x.UpdateAsync(user))
            .ReturnsAsync(IdentityResult.Success);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.Equal(ResultType.Success, result.Type);
        Assert.Equal("User logged out successfully.", result.Data);
        // Signing out ends this session only — the account may be signed in elsewhere.
        _refreshTokensMock.Verify(
            store => store.RevokeAllAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);

        _userManagerMock.Verify(x => x.FindByIdAsync(userKey), Times.Once);
        _userManagerMock.Verify(x => x.UpdateAsync(user), Times.Once);
    }

    [Fact]
    public async Task Handle_ShouldSucceed_WhenUserHasExpiredRefreshToken()
    {
        // Arrange
        var userKey = Guid.NewGuid().ToString();
        var command = new LogoutUserCommand(userKey, ["this-browser"]);
        var user = new AppUser
        {
            Id = Guid.Parse(userKey),
            Email = "expired@example.com",
            FirstName = "Expired",
            LastName = "Token"
        };

        _userManagerMock.Setup(x => x.FindByIdAsync(userKey))
            .ReturnsAsync(user);
        _userManagerMock.Setup(x => x.UpdateAsync(user))
            .ReturnsAsync(IdentityResult.Success);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.Equal(ResultType.Success, result.Type);
        // Signing out ends this session only — the account may be signed in elsewhere.
        _refreshTokensMock.Verify(
            store => store.RevokeAllAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _userManagerMock.Verify(x => x.UpdateAsync(user), Times.Once);
    }

    [Fact]
    public async Task Handle_ShouldHandleInvalidGuidUserKey()
    {
        // Arrange
        var invalidUserKey = "invalid-guid-format";
        var command = new LogoutUserCommand(invalidUserKey, ["this-browser"]);

        _userManagerMock.Setup(x => x.FindByIdAsync(invalidUserKey))
            .ReturnsAsync((AppUser?)null);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.Equal(ResultType.NotFound, result.Type);
        Assert.Equal("User not found.", result.ErrorMessage);

        _userManagerMock.Verify(x => x.FindByIdAsync(invalidUserKey), Times.Once);
        _userManagerMock.Verify(x => x.UpdateAsync(It.IsAny<AppUser>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ShouldCallUpdateAsyncOnce_WhenSuccessful()
    {
        // Arrange
        var userKey = Guid.NewGuid().ToString();
        var command = new LogoutUserCommand(userKey, ["this-browser"]);
        var user = new AppUser
        {
            Id = Guid.Parse(userKey),
            Email = "update@example.com",
            FirstName = "Update",
            LastName = "Test",
        };

        _userManagerMock.Setup(x => x.FindByIdAsync(userKey))
            .ReturnsAsync(user);
        _userManagerMock.Setup(x => x.UpdateAsync(user))
            .ReturnsAsync(IdentityResult.Success);

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        _userManagerMock.Verify(x => x.UpdateAsync(user), Times.Once);
        _userManagerMock.Verify(x => x.UpdateAsync(It.IsAny<AppUser>()), Times.Once);
    }

    [Fact]
    public void Constructor_ShouldInitializeUserManagerCorrectly()
    {
        // Arrange & Act
        var handler = new LogoutUserCommandHandler(_userManagerMock.Object, new Mock<IRefreshTokenStore>().Object);

        // Assert
        Assert.NotNull(handler);
    }
}
