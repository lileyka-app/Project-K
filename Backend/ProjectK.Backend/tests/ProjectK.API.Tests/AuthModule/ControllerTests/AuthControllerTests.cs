using System.Security.Claims;
using AutoMapper;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Moq;
using ProjectK.API.Controllers.AuthModule;
using ProjectK.API.Tests.TestHelpers;
using ProjectK.BusinessLogic.Modules.AuthModule.Features.Access.Check;
using ProjectK.BusinessLogic.Modules.AuthModule.Features.Onboarding.InviteKurinFounder;
using ProjectK.BusinessLogic.Modules.AuthModule.Features.RefreshToken.Refresh;
using ProjectK.BusinessLogic.Modules.AuthModule.Features.User.EnableMfa;
using ProjectK.BusinessLogic.Modules.AuthModule.Features.User.GenerateMfaRecoveryCodes;
using ProjectK.BusinessLogic.Modules.AuthModule.Features.User.GetMfaSetup;
using ProjectK.BusinessLogic.Modules.AuthModule.Features.User.Login;
using ProjectK.BusinessLogic.Modules.AuthModule.Features.User.Logout;
using ProjectK.BusinessLogic.Modules.AuthModule.Features.User.VerifyMfaLogin;
using ProjectK.BusinessLogic.Modules.AuthModule.Models;
using ProjectK.Common.Models.Dtos.AuthModule;
using ProjectK.Common.Models.Dtos.AuthModule.Requests;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Records;

namespace ProjectK.API.Tests.Controllers;

public class AuthControllerTests
{
    private readonly Mock<IMediator> _mediatorMock;
    private readonly Mock<IMapper> _mapperMock;
    private readonly AuthController _controller;

    public AuthControllerTests()
    {
        _mediatorMock = new Mock<IMediator>();
        _mapperMock = new Mock<IMapper>();

        _controller = new AuthController(_mediatorMock.Object, _mapperMock.Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };
    }

    [Fact]
    public async Task RegisterKurin_ShouldAnswerCreated_WithTheInvitation()
    {
        // The route takes no password any more: the founder chooses one on activation.
        var command = new InviteKurinFounderCommand("founder@example.com", "Kurin", "Founder", null, 1, null);
        var invitation = new KurinFounderInvitation(Guid.NewGuid(), Guid.NewGuid(), command.Email, InvitationSent: true);
        _mediatorMock.Setup(m => m.Send(command, default))
            .ReturnsAsync(new ServiceResult<KurinFounderInvitation>(ResultType.Created, invitation));

        var result = await _controller.RegisterKurin(command);

        var created = Assert.IsType<CreatedResult>(result);
        Assert.Equal(invitation, created.Value);
    }

    [Fact]
    public async Task Login_ShouldSetCookie_WhenLoginSuccessful()
    {
        // Arrange
        var request = new LoginUserRequest { Email = "test@test.com", Password = "pass" };

        var tokens = new JwtResponse
        {
            AccessToken = "AccessToken",
            RefreshToken = new RefreshToken
            {
                Token = "RefreshToken",
                Expires = DateTime.UtcNow.AddDays(7),
                Created = DateTime.UtcNow,
            }
        };

        var serviceResult = new ServiceResult<LoginUserResponse>(ResultType.Success, new LoginUserResponse { Tokens = tokens });

        _mapperMock.Setup(m => m.Map<LoginUserCommand>(request))
            .Returns(new LoginUserCommand(request.Email, request.Password));

        _mediatorMock.Setup(m => m.Send(It.IsAny<LoginUserCommand>(), default))
            .ReturnsAsync(serviceResult);

        // Act
        var result = await _controller.Login(request);

        // Assert
        Assert.IsType<OkObjectResult>(result);
        var setCookieHeader = _controller.Response.Headers["Set-Cookie"].ToString();
        Assert.Contains("refreshToken", setCookieHeader);
        Assert.Contains("samesite=lax", setCookieHeader, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("secure", setCookieHeader, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Login_ShouldSetSecureSameSiteNoneCookie_WhenRequestIsHttps()
    {
        // Arrange
        _controller.Request.Scheme = "https";
        var request = new LoginUserRequest { Email = "test@test.com", Password = "pass" };
        var tokens = new JwtResponse
        {
            AccessToken = "AccessToken",
            RefreshToken = new RefreshToken
            {
                Token = "RefreshToken",
                Expires = DateTime.UtcNow.AddDays(7),
                Created = DateTime.UtcNow,
            }
        };

        _mapperMock.Setup(m => m.Map<LoginUserCommand>(request))
            .Returns(new LoginUserCommand(request.Email, request.Password));

        _mediatorMock.Setup(m => m.Send(It.IsAny<LoginUserCommand>(), default))
            .ReturnsAsync(new ServiceResult<LoginUserResponse>(ResultType.Success, new LoginUserResponse { Tokens = tokens }));

        // Act
        var result = await _controller.Login(request);

        // Assert
        Assert.IsType<OkObjectResult>(result);
        var setCookieHeader = _controller.Response.Headers["Set-Cookie"].ToString();
        Assert.Contains("refreshToken", setCookieHeader);
        Assert.Contains("samesite=none", setCookieHeader, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("secure", setCookieHeader, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Login_ShouldReturnUnauthorized_WhenLoginFails()
    {
        // Arrange
        var request = new LoginUserRequest { Email = "bad@test.com", Password = "wrong" };
        var serviceResult = new ServiceResult<LoginUserResponse>(ResultType.Unauthorized);

        _mapperMock.Setup(m => m.Map<LoginUserCommand>(request))
            .Returns(new LoginUserCommand(request.Email, request.Password));

        _mediatorMock.Setup(m => m.Send(It.IsAny<LoginUserCommand>(), default))
            .ReturnsAsync(serviceResult);

        // Act
        var result = await _controller.Login(request);

        // Assert
        ApiErrorAssert.HasError(result, StatusCodes.Status401Unauthorized);
    }

    [Fact]
    public async Task Refresh_ShouldReturnUnauthorized_WhenNoCookie()
    {
        // Arrange
        // no refreshToken cookie set

        // Act
        var result = await _controller.Refresh();

        // Assert
        // Every failure now carries { error, message }; assert the contract, not the result type.
        var unauthorized = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status401Unauthorized, unauthorized.StatusCode);
    }

    [Fact]
    public async Task Refresh_ShouldDeleteCookieWithoutLogout_WhenRefreshTokenIsInvalid()
    {
        // Arrange
        _controller.Request.Headers.Cookie = "refreshToken=invalid-token";
        _mediatorMock.Setup(m => m.Send(It.IsAny<RefreshTokenCommand>(), default))
            .ReturnsAsync(new ServiceResult<RefreshTokenResponse>(ResultType.Unauthorized));

        // Act
        var result = await _controller.Refresh();

        // Assert
        // Every failure now carries { error, message }; assert the contract, not the result type.
        var unauthorized = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status401Unauthorized, unauthorized.StatusCode);
        Assert.Contains("refreshToken=", _controller.Response.Headers["Set-Cookie"].ToString());
        _mediatorMock.Verify(m => m.Send(It.IsAny<LogoutUserCommand>(), default), Times.Never);
    }

    [Fact]
    public async Task Refresh_ShouldUseValidToken_WhenDuplicateRefreshTokenCookiesExist()
    {
        // Arrange
        _controller.Request.Headers.Cookie = "refreshToken=stale-token; refreshToken=valid-token";
        var refreshedJwt = new RefreshTokenResponse
        {
            AccessToken = "new-access-token",
            RefreshToken = new RefreshToken
            {
                Token = "new-refresh-token",
                Expires = DateTime.UtcNow.AddDays(7),
                Created = DateTime.UtcNow
            }
        };

        _mediatorMock.Setup(m => m.Send(
                It.Is<RefreshTokenCommand>(c => c.RefreshToken == "stale-token"),
                default))
            .ReturnsAsync(new ServiceResult<RefreshTokenResponse>(ResultType.Unauthorized));
        _mediatorMock.Setup(m => m.Send(
                It.Is<RefreshTokenCommand>(c => c.RefreshToken == "valid-token"),
                default))
            .ReturnsAsync(new ServiceResult<RefreshTokenResponse>(ResultType.Success, refreshedJwt));

        // Act
        var result = await _controller.Refresh();

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(refreshedJwt, okResult.Value);
        Assert.Contains("new-refresh-token", _controller.Response.Headers["Set-Cookie"].ToString());
        _mediatorMock.Verify(m => m.Send(It.Is<RefreshTokenCommand>(c => c.RefreshToken == "stale-token"), default), Times.Once);
        _mediatorMock.Verify(m => m.Send(It.Is<RefreshTokenCommand>(c => c.RefreshToken == "valid-token"), default), Times.Once);
    }

    [Fact]
    public async Task CheckAccess_ShouldReturnOk_WhenMediatorReturnsTrue()
    {
        // Arrange
        var request = new CheckEntityAccessRequest
        {
            EntityType = "TestEntity",
            EntityKey = "123",
            ActiveKurinKey = Guid.NewGuid().ToString()
        };

        var serviceResult = new ServiceResult<bool>(ResultType.Success, true);

        _mediatorMock.Setup(m => m.Send(It.IsAny<CheckEntityAccessQuery>(), default))
            .ReturnsAsync(serviceResult);

        // Act
        var result = await _controller.CheckAccess(request);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var boolValue = (bool)okResult.Value!;
        Assert.True(boolValue);

        _mediatorMock.Verify(m => m.Send(
            It.Is<CheckEntityAccessQuery>(q =>
                q.EntityType == request.EntityType
                && q.EntityKey == request.EntityKey
                && q.ActiveKurinKey == null),
            default), Times.Once);
    }

    [Fact]
    public async Task GetMfaSetup_ShouldSendQueryForCurrentUser()
    {
        // Arrange
        var userKey = Guid.NewGuid();
        SetCurrentUser(userKey);
        var setup = new MfaSetupResponseDto("shared-key", "otpauth://totp/Project-K:user@example.com", "qr-base64");

        _mediatorMock.Setup(m => m.Send(It.IsAny<GetMfaSetupQuery>(), default))
            .ReturnsAsync(new ServiceResult<MfaSetupResponseDto>(ResultType.Success, setup));

        // Act
        var result = await _controller.GetMfaSetup();

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(setup, okResult.Value);
        _mediatorMock.Verify(m => m.Send(
            It.Is<GetMfaSetupQuery>(q => q.UserKey == userKey),
            default), Times.Once);
    }

    [Fact]
    public async Task EnableMfa_ShouldSendCommandForCurrentUser()
    {
        // Arrange
        var userKey = Guid.NewGuid();
        SetCurrentUser(userKey);
        var request = new MfaVerifyRequestDto("123 456");
        var response = new MfaEnableResponseDto(true, new[] { "code-1", "code-2" });

        _mediatorMock.Setup(m => m.Send(It.IsAny<EnableMfaCommand>(), default))
            .ReturnsAsync(new ServiceResult<MfaEnableResponseDto>(ResultType.Success, response));

        // Act
        var result = await _controller.EnableMfa(request);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(response, okResult.Value);
        _mediatorMock.Verify(m => m.Send(
            It.Is<EnableMfaCommand>(cmd => cmd.UserKey == userKey && cmd.Code == request.Code),
            default), Times.Once);
    }

    [Fact]
    public async Task EnableMfa_ShouldSetTheRefreshCookie_ForTheSessionThatReplacesTheEndedOnes()
    {
        // Arrange
        SetCurrentUser(Guid.NewGuid());
        var tokens = new JwtResponse
        {
            AccessToken = "fresh-access",
            RefreshToken = new RefreshToken { Token = "fresh-refresh", Expires = DateTime.UtcNow.AddDays(7) }
        };
        _mediatorMock.Setup(m => m.Send(It.IsAny<EnableMfaCommand>(), default))
            .ReturnsAsync(new ServiceResult<MfaEnableResponseDto>(ResultType.Success, new MfaEnableResponseDto(true, [], tokens)));

        // Act
        var result = await _controller.EnableMfa(new MfaVerifyRequestDto("123456"));

        // Assert
        Assert.IsType<OkObjectResult>(result);
        Assert.Contains("fresh-refresh", _controller.Response.Headers["Set-Cookie"].ToString());
    }

    [Fact]
    public async Task RotateMfaRecoveryCodes_ShouldSendCommandForCurrentUser()
    {
        // Arrange
        var userKey = Guid.NewGuid();
        SetCurrentUser(userKey);
        var request = new MfaRecoveryCodesRequestDto("current-password");
        var response = new MfaRecoveryCodesResponseDto(new[] { "code-1" });

        _mediatorMock.Setup(m => m.Send(It.IsAny<GenerateMfaRecoveryCodesCommand>(), default))
            .ReturnsAsync(new ServiceResult<MfaRecoveryCodesResponseDto>(ResultType.Success, response));

        // Act
        var result = await _controller.RotateMfaRecoveryCodes(request);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(response, okResult.Value);
        _mediatorMock.Verify(m => m.Send(
            It.Is<GenerateMfaRecoveryCodesCommand>(cmd =>
                cmd.UserKey == userKey && cmd.CurrentPassword == request.CurrentPassword),
            default), Times.Once);
    }

    [Fact]
    public async Task VerifyMfaLogin_ShouldSetCookie_WhenVerificationReturnsTokens()
    {
        // Arrange
        var request = new MfaLoginRequestDto("user@example.com", "123456", true);
        var tokens = new JwtResponse
        {
            AccessToken = "AccessToken",
            RefreshToken = new RefreshToken
            {
                Token = "MfaRefreshToken",
                Expires = DateTime.UtcNow.AddDays(7),
                Created = DateTime.UtcNow
            }
        };
        var response = new LoginUserResponse { Email = request.Email, Tokens = tokens };

        _mediatorMock.Setup(m => m.Send(It.IsAny<VerifyMfaLoginCommand>(), default))
            .ReturnsAsync(new ServiceResult<LoginUserResponse>(ResultType.Success, response));

        // Act
        var result = await _controller.VerifyMfaLogin(request);

        // Assert
        Assert.IsType<OkObjectResult>(result);
        Assert.Contains("refreshToken=MfaRefreshToken", _controller.Response.Headers["Set-Cookie"].ToString());
        _mediatorMock.Verify(m => m.Send(
            It.Is<VerifyMfaLoginCommand>(cmd =>
                cmd.Email == request.Email &&
                cmd.Code == request.Code &&
                cmd.RememberMe == request.RememberMe),
            default), Times.Once);
    }

    [Fact]
    public async Task VerifyMfaLogin_ShouldNotSetCookie_WhenMfaIsUnauthorized()
    {
        // Arrange
        var request = new MfaLoginRequestDto("user@example.com", "bad-code", false);

        _mediatorMock.Setup(m => m.Send(It.IsAny<VerifyMfaLoginCommand>(), default))
            .ReturnsAsync(new ServiceResult<LoginUserResponse>(ResultType.Unauthorized));

        // Act
        var result = await _controller.VerifyMfaLogin(request);

        // Assert
        ApiErrorAssert.HasError(result, StatusCodes.Status401Unauthorized);
        Assert.DoesNotContain("refreshToken", _controller.Response.Headers["Set-Cookie"].ToString());
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
