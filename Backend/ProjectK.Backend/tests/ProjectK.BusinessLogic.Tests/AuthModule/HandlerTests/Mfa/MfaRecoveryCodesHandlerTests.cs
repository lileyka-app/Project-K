using Microsoft.AspNetCore.Identity;
using ProjectK.Common.Models.Records;
using Microsoft.Extensions.Logging;
using Moq;
using ProjectK.BusinessLogic.Modules.AuthModule.Features.User.GenerateMfaRecoveryCodes;
using ProjectK.BusinessLogic.Modules.AuthModule.Features.User.VerifyMfaLogin;
using ProjectK.BusinessLogic.Modules.AuthModule.Models;
using ProjectK.BusinessLogic.Modules.AuthModule.Services;
using ProjectK.Common.Entities.AuthModule;
using ProjectK.Common.Interfaces.Modules.InfrastructureModule;
using ProjectK.Common.Models.Enums;

namespace ProjectK.BusinessLogic.Tests.AuthModule.HandlerTests.Mfa;

public class MfaRecoveryCodesHandlerTests
{
    private readonly Mock<UserManager<AppUser>> _userManagerMock;

    public MfaRecoveryCodesHandlerTests()
    {
        var userStoreMock = new Mock<IUserStore<AppUser>>();
        _userManagerMock = new Mock<UserManager<AppUser>>(
            userStoreMock.Object, null, null, null, null, null, null, null, null);
    }

    [Fact]
    public async Task VerifyMfaLogin_ShouldRedeemRecoveryCode_WhenTotpIsInvalid()
    {
        // Arrange
        var user = new AppUser
        {
            Id = Guid.NewGuid(),
            Email = "user@example.com",
            FirstName = "John",
            LastName = "Doe"
        };
        var response = new LoginUserResponse { UserKey = user.Id, Email = user.Email };
        var loginResponseFactoryMock = new Mock<ILoginResponseFactory>();
        var provider = _userManagerMock.Object.Options.Tokens.AuthenticatorTokenProvider;

        _userManagerMock.Setup(x => x.FindByEmailAsync(user.Email)).ReturnsAsync(user);
        _userManagerMock.Setup(x => x.VerifyTwoFactorTokenAsync(user, provider, "recoverycode"))
            .ReturnsAsync(false);
        _userManagerMock.Setup(x => x.RedeemTwoFactorRecoveryCodeAsync(user, "recovery-code"))
            .ReturnsAsync(IdentityResult.Success);
        loginResponseFactoryMock.Setup(x => x.CreateAsync(user, It.IsAny<CancellationToken>()))
            .ReturnsAsync(response);

        var handler = CreateVerifyHandler(loginResponseFactoryMock.Object, challengeFor: user.Id);

        // Act
        var result = await handler.Handle(new VerifyMfaLoginCommand(user.Email, "recovery-code", true, "challenge"), CancellationToken.None);

        // Assert
        Assert.Equal(ResultType.Success, result.Type);
        Assert.Same(response, result.Data);
        _userManagerMock.Verify(x => x.RedeemTwoFactorRecoveryCodeAsync(user, "recovery-code"), Times.Once);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("challenge-for-somebody-else")]
    public async Task VerifyMfaLogin_ShouldRefuse_WithoutTheChallengeThePasswordStepIssued(string? mfaToken)
    {
        // A valid code on its own is not a sign-in: the second factor is only ever the second one.
        var user = new AppUser
        {
            Id = Guid.NewGuid(),
            Email = "user@example.com",
            FirstName = "John",
            LastName = "Doe"
        };
        var loginResponseFactoryMock = new Mock<ILoginResponseFactory>();
        var provider = _userManagerMock.Object.Options.Tokens.AuthenticatorTokenProvider;

        _userManagerMock.Setup(x => x.FindByEmailAsync(user.Email)).ReturnsAsync(user);
        _userManagerMock.Setup(x => x.VerifyTwoFactorTokenAsync(user, provider, "123456")).ReturnsAsync(true);

        var handler = CreateVerifyHandler(loginResponseFactoryMock.Object, challengeFor: Guid.NewGuid());

        var result = await handler.Handle(new VerifyMfaLoginCommand(user.Email, "123456", true, mfaToken), CancellationToken.None);

        Assert.Equal(ResultType.Unauthorized, result.Type);
        Assert.Equal("InvalidCredentials", result.ErrorCode);
        loginResponseFactoryMock.Verify(x => x.CreateAsync(It.IsAny<AppUser>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>
    /// The code was right once on this device; the answer carries what the browser keeps so that
    /// the next sign-ins here skip the second factor.
    /// </summary>
    [Fact]
    public async Task VerifyMfaLogin_ShouldHandBackDeviceTrust_WhenTheCodeIsRight()
    {
        var user = new AppUser
        {
            Id = Guid.NewGuid(),
            Email = "user@example.com",
            FirstName = "John",
            LastName = "Doe",
            SecurityStamp = "stamp-1",
            OnboardingStatus = OnboardingStatus.Active
        };
        var loginResponseFactoryMock = new Mock<ILoginResponseFactory>();
        loginResponseFactoryMock
            .Setup(x => x.CreateAsync(user, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LoginUserResponse { UserKey = user.Id, Email = user.Email });
        var provider = _userManagerMock.Object.Options.Tokens.AuthenticatorTokenProvider;
        _userManagerMock.Setup(x => x.FindByEmailAsync(user.Email)).ReturnsAsync(user);
        _userManagerMock.Setup(x => x.VerifyTwoFactorTokenAsync(user, provider, "123456")).ReturnsAsync(true);
        var handler = CreateVerifyHandler(loginResponseFactoryMock.Object, challengeFor: user.Id);

        var result = await handler.Handle(new VerifyMfaLoginCommand(user.Email, "123456", true, "challenge"), CancellationToken.None);

        Assert.Equal(ResultType.Success, result.Type);
        Assert.Equal("device-trust", result.Data!.MfaTrust!.Token);
    }

    /// <summary>SEC-4.3: suspended between the password step and this one is still suspended.</summary>
    [Fact]
    public async Task VerifyMfaLogin_ShouldRefuse_WhenTheAccountIsSuspended()
    {
        var user = new AppUser
        {
            Id = Guid.NewGuid(),
            Email = "user@example.com",
            FirstName = "John",
            LastName = "Doe",
            OnboardingStatus = OnboardingStatus.Suspended
        };
        var loginResponseFactoryMock = new Mock<ILoginResponseFactory>();
        var provider = _userManagerMock.Object.Options.Tokens.AuthenticatorTokenProvider;
        _userManagerMock.Setup(x => x.FindByEmailAsync(user.Email)).ReturnsAsync(user);
        _userManagerMock.Setup(x => x.VerifyTwoFactorTokenAsync(user, provider, "123456")).ReturnsAsync(true);
        var handler = CreateVerifyHandler(loginResponseFactoryMock.Object, challengeFor: user.Id);

        var result = await handler.Handle(new VerifyMfaLoginCommand(user.Email, "123456", true, "challenge"), CancellationToken.None);

        Assert.Equal(ResultType.Unauthorized, result.Type);
        Assert.Equal("InvalidCredentials", result.ErrorCode);
        loginResponseFactoryMock.Verify(x => x.CreateAsync(It.IsAny<AppUser>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private VerifyMfaLoginCommandHandler CreateVerifyHandler(ILoginResponseFactory loginResponseFactory, Guid challengeFor)
    {
        var jwtServiceMock = new Mock<IJwtService>();
        jwtServiceMock.Setup(x => x.ReadMfaChallenge("challenge")).Returns(challengeFor);
        jwtServiceMock.Setup(x => x.ReadMfaChallenge("challenge-for-somebody-else")).Returns(Guid.NewGuid());
        jwtServiceMock
            .Setup(x => x.GenerateMfaTrustToken(challengeFor, It.IsAny<string>()))
            .Returns(new MfaTrustGrant("device-trust", DateTime.UtcNow.AddDays(7)));

        return new VerifyMfaLoginCommandHandler(
            _userManagerMock.Object,
            loginResponseFactory,
            new Mock<ILogger<VerifyMfaLoginCommandHandler>>().Object,
            new Mock<IActivityLogger>().Object,
            jwtServiceMock.Object);
    }

    [Fact]
    public async Task GenerateMfaRecoveryCodes_ShouldRequireCurrentPassword()
    {
        // Arrange
        var user = new AppUser
        {
            Id = Guid.NewGuid(),
            Email = "user@example.com",
            FirstName = "John",
            LastName = "Doe",
            TwoFactorEnabled = true
        };

        _userManagerMock.Setup(x => x.FindByIdAsync(user.Id.ToString())).ReturnsAsync(user);
        _userManagerMock.Setup(x => x.CheckPasswordAsync(user, "wrong-password")).ReturnsAsync(false);

        var handler = new GenerateMfaRecoveryCodesCommandHandler(
            _userManagerMock.Object,
            new Mock<IActivityLogger>().Object);

        // Act
        var result = await handler.Handle(
            new GenerateMfaRecoveryCodesCommand(user.Id, "wrong-password"),
            CancellationToken.None);

        // Assert
        Assert.Equal(ResultType.Unauthorized, result.Type);
        _userManagerMock.Verify(x => x.GenerateNewTwoFactorRecoveryCodesAsync(It.IsAny<AppUser>(), It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public async Task GenerateMfaRecoveryCodes_ShouldRotateCodes_WhenPasswordIsValid()
    {
        // Arrange
        var user = new AppUser
        {
            Id = Guid.NewGuid(),
            Email = "user@example.com",
            FirstName = "John",
            LastName = "Doe",
            TwoFactorEnabled = true
        };
        var codes = new[] { "code-1", "code-2" };

        _userManagerMock.Setup(x => x.FindByIdAsync(user.Id.ToString())).ReturnsAsync(user);
        _userManagerMock.Setup(x => x.CheckPasswordAsync(user, "current-password")).ReturnsAsync(true);
        _userManagerMock.Setup(x => x.GenerateNewTwoFactorRecoveryCodesAsync(user, 10)).ReturnsAsync(codes);

        var handler = new GenerateMfaRecoveryCodesCommandHandler(
            _userManagerMock.Object,
            new Mock<IActivityLogger>().Object);

        // Act
        var result = await handler.Handle(
            new GenerateMfaRecoveryCodesCommand(user.Id, "current-password"),
            CancellationToken.None);

        // Assert
        Assert.Equal(ResultType.Success, result.Type);
        Assert.Equal(codes, result.Data?.RecoveryCodes);
    }
}
