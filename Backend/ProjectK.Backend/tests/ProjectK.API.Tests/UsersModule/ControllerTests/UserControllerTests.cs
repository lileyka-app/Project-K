using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using ProjectK.API.Controllers.UsersModule;
using ProjectK.API.Tests.TestHelpers;
using ProjectK.BusinessLogic.Modules.AuthModule.Features.Mfa;
using ProjectK.BusinessLogic.Modules.UsersModule.Features.Account;
using ProjectK.BusinessLogic.Modules.UsersModule.Features.User;
using ProjectK.Common.Dtos.UsersModule;
using ProjectK.Common.Enums.KurinModule;
using ProjectK.Common.Enums.Shared;
using ProjectK.Common.Records.Shared;

namespace ProjectK.API.Tests.UsersModule.ControllerTests;

public class UserControllerTests
{
    private readonly Mock<IMediator> _mediatorMock;
    private readonly UserController _controller;

    public UserControllerTests()
    {
        _mediatorMock = new Mock<IMediator>();
        _controller = new UserController(_mediatorMock.Object);
    }

    [Fact]
    public async Task GetAllUsers_ShouldReturnOk_WhenSuccess()
    {
        // Arrange
        var users = new List<UserDto>
        {
            new UserDto
            {
                UserId = Guid.NewGuid(),
                KurinKey = Guid.NewGuid(),
                FirstName = "John",
                LastName = "Doe",
                Email = "john.doe@example.com",
                Role = "User",
                KurinNumber = null,
                KurinBranch = null,
                KurinBranches = null,
                KurinNamedAfter = null,
                KurinKraiCode = null,
                TwoFactorEnabled = false,
                IsSuspended = false
            },
            new UserDto
            {
                UserId = Guid.NewGuid(),
                KurinKey = Guid.NewGuid(),
                FirstName = "Jane",
                LastName = "Smith",
                Email = "jane.smith@example.com",
                Role = "Admin",
                KurinNumber = null,
                KurinBranch = null,
                KurinBranches = null,
                KurinNamedAfter = null,
                KurinKraiCode = null,
                TwoFactorEnabled = false,
                IsSuspended = false
            }
        };
        var serviceResult = new ServiceResult<IEnumerable<UserDto>>(ResultType.Success, users);

        _mediatorMock
            .Setup(m => m.Send(It.IsAny<GetAllUsersQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(serviceResult);

        // Act
        var result = await _controller.GetAllUsers();

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var returnedUsers = Assert.IsAssignableFrom<IEnumerable<UserDto>>(okResult.Value);
        Assert.Equal(users.Count, returnedUsers.Count());

        _mediatorMock.Verify(m => m.Send(It.IsAny<GetAllUsersQuery>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetAllUsers_ShouldReturnNotFound_WhenNotFound()
    {
        // Arrange
        var serviceResult = new ServiceResult<IEnumerable<UserDto>>(ResultType.NotFound);

        _mediatorMock
            .Setup(m => m.Send(It.IsAny<GetAllUsersQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(serviceResult);

        // Act
        var result = await _controller.GetAllUsers();

        // Assert
        Assert.IsType<NotFoundObjectResult>(result);
        _mediatorMock.Verify(m => m.Send(It.IsAny<GetAllUsersQuery>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetAllUsers_ShouldReturnBadRequest_WhenBadRequest()
    {
        // Arrange
        var serviceResult = new ServiceResult<IEnumerable<UserDto>>(ResultType.BadRequest, null);

        _mediatorMock
            .Setup(m => m.Send(It.IsAny<GetAllUsersQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(serviceResult);

        // Act
        var result = await _controller.GetAllUsers();

        // Assert
        Assert.IsType<BadRequestObjectResult>(result);
        _mediatorMock.Verify(m => m.Send(It.IsAny<GetAllUsersQuery>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetAllUsers_ShouldReturnUnauthorized_WhenUnauthorized()
    {
        // Arrange
        var serviceResult = new ServiceResult<IEnumerable<UserDto>>(ResultType.Unauthorized);

        _mediatorMock
            .Setup(m => m.Send(It.IsAny<GetAllUsersQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(serviceResult);

        // Act
        var result = await _controller.GetAllUsers();

        // Assert
        ApiErrorAssert.HasError(result, StatusCodes.Status401Unauthorized);
        _mediatorMock.Verify(m => m.Send(It.IsAny<GetAllUsersQuery>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetAllUsers_ShouldReturnConflict_WhenConflict()
    {
        // Arrange
        var serviceResult = new ServiceResult<IEnumerable<UserDto>>(ResultType.Conflict, null);

        _mediatorMock
            .Setup(m => m.Send(It.IsAny<GetAllUsersQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(serviceResult);

        // Act
        var result = await _controller.GetAllUsers();

        // Assert
        ApiErrorAssert.HasError(result, StatusCodes.Status409Conflict);
        _mediatorMock.Verify(m => m.Send(It.IsAny<GetAllUsersQuery>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetAllUsers_ShouldReturnInternalServerError_WhenUnexpectedResultType()
    {
        // Arrange
        var serviceResult = new ServiceResult<IEnumerable<UserDto>>((ResultType)999); // Unknown result type

        _mediatorMock
            .Setup(m => m.Send(It.IsAny<GetAllUsersQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(serviceResult);

        // Act
        var result = await _controller.GetAllUsers();

        // Assert
        ApiErrorAssert.HasError(result, StatusCodes.Status500InternalServerError);
        _mediatorMock.Verify(m => m.Send(It.IsAny<GetAllUsersQuery>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetAllUsers_ShouldReturnOkWithEmptyList_WhenNoUsers()
    {
        // Arrange
        var emptyUsers = new List<UserDto>();
        var serviceResult = new ServiceResult<IEnumerable<UserDto>>(ResultType.Success, emptyUsers);

        _mediatorMock
            .Setup(m => m.Send(It.IsAny<GetAllUsersQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(serviceResult);

        // Act
        var result = await _controller.GetAllUsers();

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var returnedUsers = Assert.IsAssignableFrom<IEnumerable<UserDto>>(okResult.Value);
        Assert.Empty(returnedUsers);
        _mediatorMock.Verify(m => m.Send(It.IsAny<GetAllUsersQuery>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateAccountProfile_ShouldSendCommandForCurrentUser()
    {
        // Arrange
        var userKey = Guid.NewGuid();
        SetCurrentUser(userKey);
        var request = new UpdateAccountProfileRequestDto("new@example.com", "123456789", "current-password");
        var settings = new AccountSettingsDto(userKey, null, "current@example.com", "123456789", "John", "Doe", "User", false, "new@example.com");

        _mediatorMock
            .Setup(m => m.Send(It.IsAny<UpdateAccountProfileCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ServiceResult<AccountSettingsDto>(ResultType.Success, settings));

        // Act
        var result = await _controller.UpdateAccountProfile(request);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var returnedSettings = Assert.IsType<AccountSettingsDto>(okResult.Value);
        Assert.Equal("new@example.com", returnedSettings.PendingEmail);

        _mediatorMock.Verify(m => m.Send(It.Is<UpdateAccountProfileCommand>(cmd =>
            cmd.UserKey == userKey &&
            cmd.Email == request.Email &&
            cmd.PhoneNumber == request.PhoneNumber &&
            cmd.CurrentPassword == request.CurrentPassword), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetAccountSettings_ShouldSendQueryForCurrentUser()
    {
        // Arrange
        var userKey = Guid.NewGuid();
        SetCurrentUser(userKey);
        var settings = new AccountSettingsDto(userKey, null, "user@example.com", "123456789", "John", "Doe", "User", true);

        _mediatorMock
            .Setup(m => m.Send(It.IsAny<GetAccountSettingsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ServiceResult<AccountSettingsDto>(ResultType.Success, settings));

        // Act
        var result = await _controller.GetAccountSettings();

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(settings, okResult.Value);

        _mediatorMock.Verify(m => m.Send(It.Is<GetAccountSettingsQuery>(query =>
            query.UserKey == userKey), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ChangePassword_ShouldSendCommandForCurrentUser()
    {
        // Arrange
        var userKey = Guid.NewGuid();
        SetCurrentUser(userKey);
        var request = new ChangePasswordRequestDto("current-password", "new-password");

        _mediatorMock
            .Setup(m => m.Send(It.IsAny<ChangeOwnPasswordCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ServiceResult<Unit>(ResultType.Success, Unit.Value));

        // Act
        var result = await _controller.ChangePassword(request);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.Null(okResult.Value);

        _mediatorMock.Verify(m => m.Send(It.Is<ChangeOwnPasswordCommand>(cmd =>
            cmd.UserKey == userKey &&
            cmd.CurrentPassword == request.CurrentPassword &&
            cmd.NewPassword == request.NewPassword), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ResetMfa_ShouldSendCommandForCurrentUser()
    {
        // Arrange
        var userKey = Guid.NewGuid();
        SetCurrentUser(userKey);
        var request = new ResetMfaRequestDto("current-password");

        _mediatorMock
            .Setup(m => m.Send(It.IsAny<ResetOwnMfaCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ServiceResult<Unit>(ResultType.Success, Unit.Value));

        // Act
        var result = await _controller.ResetMfa(request);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.Null(okResult.Value);

        _mediatorMock.Verify(m => m.Send(It.Is<ResetOwnMfaCommand>(cmd =>
            cmd.UserKey == userKey &&
            cmd.CurrentPassword == request.CurrentPassword), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DisableMfa_ShouldSendCommandForCurrentUser()
    {
        // Arrange
        var userKey = Guid.NewGuid();
        SetCurrentUser(userKey);
        var request = new DisableMfaRequestDto("current-password");

        _mediatorMock
            .Setup(m => m.Send(It.IsAny<DisableOwnMfaCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ServiceResult<Unit>(ResultType.Success, Unit.Value));

        // Act
        var result = await _controller.DisableMfa(request);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.Null(okResult.Value);

        _mediatorMock.Verify(m => m.Send(It.Is<DisableOwnMfaCommand>(cmd =>
            cmd.UserKey == userKey &&
            cmd.CurrentPassword == request.CurrentPassword), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ResetUserMfa_ShouldSendCommandForTargetUser()
    {
        // Arrange
        var targetUserKey = Guid.NewGuid();

        _mediatorMock
            .Setup(m => m.Send(It.IsAny<ResetUserMfaCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ServiceResult<Unit>(ResultType.Success, Unit.Value));

        // Act
        var result = await _controller.ResetUserMfa(targetUserKey);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.Null(okResult.Value);

        _mediatorMock.Verify(m => m.Send(It.Is<ResetUserMfaCommand>(cmd =>
            cmd.TargetUserKey == targetUserKey), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SuspendUser_ShouldSendCommandForTargetUser()
    {
        var targetUserKey = Guid.NewGuid();
        _mediatorMock
            .Setup(m => m.Send(It.IsAny<SuspendUserCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ServiceResult<Unit>(ResultType.Success, Unit.Value));

        var result = await _controller.SuspendUser(targetUserKey);

        Assert.IsType<OkObjectResult>(result);
        _mediatorMock.Verify(m => m.Send(It.Is<SuspendUserCommand>(cmd =>
            cmd.TargetUserKey == targetUserKey), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RestoreUser_ShouldSendCommandForTargetUser()
    {
        var targetUserKey = Guid.NewGuid();
        _mediatorMock
            .Setup(m => m.Send(It.IsAny<RestoreUserCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ServiceResult<Unit>(ResultType.Success, Unit.Value));

        var result = await _controller.RestoreUser(targetUserKey);

        Assert.IsType<OkObjectResult>(result);
        _mediatorMock.Verify(m => m.Send(It.Is<RestoreUserCommand>(cmd =>
            cmd.TargetUserKey == targetUserKey), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ConfirmAccountEmailChange_ShouldSendCommandForCurrentUser()
    {
        // Arrange
        var userKey = Guid.NewGuid();
        SetCurrentUser(userKey);
        var request = new ConfirmAccountEmailChangeRequestDto("confirmed@example.com", "token");
        var settings = new AccountSettingsDto(userKey, null, request.Email, null, "John", "Doe", "User", false);

        _mediatorMock
            .Setup(m => m.Send(It.IsAny<ConfirmAccountEmailChangeCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ServiceResult<AccountSettingsDto>(ResultType.Success, settings));

        // Act
        var result = await _controller.ConfirmAccountEmailChange(request);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var returnedSettings = Assert.IsType<AccountSettingsDto>(okResult.Value);
        Assert.Equal(request.Email, returnedSettings.Email);

        _mediatorMock.Verify(m => m.Send(It.Is<ConfirmAccountEmailChangeCommand>(cmd =>
            cmd.UserKey == userKey &&
            cmd.Email == request.Email &&
            cmd.Token == request.Token), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void Constructor_ShouldInitializeMediatorCorrectly()
    {
        // Arrange & Act
        var controller = new UserController(_mediatorMock.Object);

        // Assert
        Assert.NotNull(controller);
    }

    private void SetCurrentUser(Guid userKey)
    {
        var claims = new[] { new Claim(ClaimTypes.NameIdentifier, userKey.ToString()) };
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(claims))
            }
        };
    }
}
