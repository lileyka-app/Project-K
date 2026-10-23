using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Moq;
using ProjectK.BusinessLogic.Tests.TestHelpers;
using ProjectK.BusinessLogic.Modules.UsersModule.Features.Account.ConfirmEmailChange;
using ProjectK.BusinessLogic.Modules.UsersModule.Features.Account.Get;
using ProjectK.BusinessLogic.Modules.UsersModule.Features.Account.UpdateProfile;
using ProjectK.Common.Entities.AuthModule;
using ProjectK.Common.Entities.KurinModule;
using ProjectK.Common.Interfaces;
using ProjectK.Common.Interfaces.Modules.AuthModule;
using ProjectK.Common.Interfaces.Modules.InfrastructureModule;
using ProjectK.Common.Interfaces.Modules.KurinModule;
using ProjectK.Common.Interfaces.Modules.MemberModule;
using ProjectK.Common.Models.Dtos.AuthModule;
using ProjectK.Common.Models.Dtos.UsersModule;
using ProjectK.Common.Models.Dtos.UsersModule;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Records;
using ProjectK.Common.Models.Settings;

namespace ProjectK.BusinessLogic.Tests.UsersModule.HandlerTests;

public class AccountEmailConfirmationHandlerTests
{
    private readonly Mock<UserManager<AppUser>> _userManagerMock;
    private readonly Mock<IRefreshTokenStore> _refreshTokensMock = new();
    private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();
    private readonly Mock<IMemberDirectory> _memberDirectoryMock = new();
    private readonly Mock<IMediator> _mediatorMock = new();
    private readonly Mock<IEmailService> _emailServiceMock = new();

    public AccountEmailConfirmationHandlerTests()
    {
        var userStoreMock = new Mock<IUserStore<AppUser>>();
        _userManagerMock = new Mock<UserManager<AppUser>>(
            userStoreMock.Object, null, null, null, null, null, null, null, null);
    }

    [Fact]
    public async Task UpdateAccountProfile_ShouldSendConfirmationEmailAndKeepExistingEmail_WhenEmailChanges()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var currentEmail = "old@example.com";
        var newEmail = "new@example.com";
        var token = "token+with/special==";
        var member = new Member
        {
            UserKey = userId,
            Email = currentEmail,
            PhoneNumber = "+380501111111",
            FirstName = "John",
            LastName = "Doe"
        };

        var user = new AppUser
        {
            Id = userId,
            Email = currentEmail,
            UserName = currentEmail,
            PhoneNumber = "+380501111111",
            FirstName = "John",
            LastName = "Doe"
        };

        var settings = new AccountSettingsDto(
            userId,
            member.MemberKey,
            currentEmail,
            "+380502222222",
            "John",
            "Doe",
            "User",
            false);

        _userManagerMock.Setup(x => x.FindByIdAsync(userId.ToString())).ReturnsAsync(user);
        _userManagerMock.Setup(x => x.FindByEmailAsync(newEmail)).ReturnsAsync((AppUser?)null);
        _userManagerMock.Setup(x => x.CheckPasswordAsync(user, "current-password")).ReturnsAsync(true);
        _userManagerMock.Setup(x => x.UpdateAsync(user)).ReturnsAsync(IdentityResult.Success);
        _userManagerMock.Setup(x => x.GenerateChangeEmailTokenAsync(user, newEmail)).ReturnsAsync(token);
        _unitOfWorkMock.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
        _mediatorMock.Setup(x => x.Send(It.Is<GetAccountSettingsQuery>(q => q.UserKey == userId), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ServiceResult<AccountSettingsDto>(ResultType.Success, settings));

        var handler = CreateUpdateHandler();

        // Act
        var result = await handler.Handle(new UpdateAccountProfileCommand(userId, $" {newEmail} ", " +380 50 222 22 22 ", "current-password"), CancellationToken.None);

        // Assert
        Assert.Equal(ResultType.Success, result.Type);
        Assert.NotNull(result.Data);
        Assert.Equal(newEmail, result.Data.PendingEmail);
        Assert.Equal(currentEmail, user.Email);
        Assert.Equal(currentEmail, user.UserName);
        Assert.Equal("+380502222222", user.PhoneNumber);
        _memberDirectoryMock.Verify(
            x => x.SetPhoneFromAccountAsync(userId, "+380502222222", It.IsAny<CancellationToken>()),
            Times.Once);
        _memberDirectoryMock.Verify(
            x => x.SetEmailFromAccountAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);

        _emailServiceMock.Verify(x => x.SendEmailChangeConfirmationEmailAsync(
            newEmail,
            currentEmail,
            It.Is<string>(url =>
                url.StartsWith("http://localhost:4200/settings/account?confirmEmail=true") &&
                url.Contains("email=new%40example.com") &&
                url.Contains("token=token%2Bwith%2Fspecial%3D%3D")),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateAccountProfile_ShouldReturnUnauthorized_WhenEmailChangesWithoutCurrentPassword()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = new AppUser
        {
            Id = userId,
            Email = "old@example.com",
            UserName = "old@example.com",
            FirstName = "John",
            LastName = "Doe"
        };

        _userManagerMock.Setup(x => x.FindByIdAsync(userId.ToString())).ReturnsAsync(user);
        _userManagerMock.Setup(x => x.FindByEmailAsync("new@example.com")).ReturnsAsync((AppUser?)null);

        var handler = CreateUpdateHandler();

        // Act
        var result = await handler.Handle(new UpdateAccountProfileCommand(userId, "new@example.com", null), CancellationToken.None);

        // Assert
        Assert.Equal(ResultType.Unauthorized, result.Type);
        _userManagerMock.Verify(x => x.CheckPasswordAsync(It.IsAny<AppUser>(), It.IsAny<string>()), Times.Never);
        _userManagerMock.Verify(x => x.UpdateAsync(It.IsAny<AppUser>()), Times.Never);
        _emailServiceMock.Verify(x => x.SendEmailChangeConfirmationEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UpdateAccountProfile_ShouldReturnUnauthorized_WhenEmailChangesWithWrongCurrentPassword()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = new AppUser
        {
            Id = userId,
            Email = "old@example.com",
            UserName = "old@example.com",
            FirstName = "John",
            LastName = "Doe"
        };

        _userManagerMock.Setup(x => x.FindByIdAsync(userId.ToString())).ReturnsAsync(user);
        _userManagerMock.Setup(x => x.FindByEmailAsync("new@example.com")).ReturnsAsync((AppUser?)null);
        _userManagerMock.Setup(x => x.CheckPasswordAsync(user, "wrong-password")).ReturnsAsync(false);

        var handler = CreateUpdateHandler();

        // Act
        var result = await handler.Handle(new UpdateAccountProfileCommand(userId, "new@example.com", null, "wrong-password"), CancellationToken.None);

        // Assert
        Assert.Equal(ResultType.Unauthorized, result.Type);
        _userManagerMock.Verify(x => x.UpdateAsync(It.IsAny<AppUser>()), Times.Never);
        _emailServiceMock.Verify(x => x.SendEmailChangeConfirmationEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UpdateAccountProfile_ShouldNotSendConfirmationEmail_WhenEmailIsUnchanged()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var email = "user@example.com";
        var user = new AppUser
        {
            Id = userId,
            Email = email,
            UserName = email,
            PhoneNumber = "+380501111111",
            FirstName = "John",
            LastName = "Doe"
        };
        var settings = new AccountSettingsDto(userId, null, email, "+380502222222", "John", "Doe", "User", false);

        _userManagerMock.Setup(x => x.FindByIdAsync(userId.ToString())).ReturnsAsync(user);
        _userManagerMock.Setup(x => x.FindByEmailAsync(email)).ReturnsAsync(user);
        _userManagerMock.Setup(x => x.UpdateAsync(user)).ReturnsAsync(IdentityResult.Success);
        _mediatorMock.Setup(x => x.Send(It.Is<GetAccountSettingsQuery>(q => q.UserKey == userId), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ServiceResult<AccountSettingsDto>(ResultType.Success, settings));

        var handler = CreateUpdateHandler();

        // Act
        var result = await handler.Handle(new UpdateAccountProfileCommand(userId, "USER@example.com", " +380 50 222 22 22 "), CancellationToken.None);

        // Assert
        Assert.Equal(ResultType.Success, result.Type);
        Assert.Null(result.Data?.PendingEmail);
        Assert.Equal(email, user.Email);
        Assert.Equal("+380502222222", user.PhoneNumber);
        _userManagerMock.Verify(x => x.GenerateChangeEmailTokenAsync(It.IsAny<AppUser>(), It.IsAny<string>()), Times.Never);
        _emailServiceMock.Verify(x => x.SendEmailChangeConfirmationEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ConfirmAccountEmailChange_ShouldUpdateUserAndTrackedMember_WhenTokenIsValid()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var oldEmail = "old@example.com";
        var newEmail = "new@example.com";
        var token = "valid-token";
        var user = new AppUser
        {
            Id = userId,
            Email = oldEmail,
            UserName = oldEmail,
            NormalizedUserName = "OLD@EXAMPLE.COM",
            FirstName = "John",
            LastName = "Doe",
        };
        var member = new Member
        {
            UserKey = userId,
            Email = oldEmail,
            FirstName = "John",
            LastName = "Doe"
        };
        var settings = new AccountSettingsDto(userId, member.MemberKey, newEmail, null, "John", "Doe", "User", false);

        _userManagerMock.Setup(x => x.FindByIdAsync(userId.ToString())).ReturnsAsync(user);
        _userManagerMock.Setup(x => x.FindByEmailAsync(newEmail)).ReturnsAsync((AppUser?)null);
        _userManagerMock.Setup(x => x.ChangeEmailAsync(user, newEmail, token))
            .Callback(() => user.Email = newEmail)
            .ReturnsAsync(IdentityResult.Success);
        _userManagerMock.Setup(x => x.NormalizeName(newEmail)).Returns("NEW@EXAMPLE.COM");
        _userManagerMock.Setup(x => x.UpdateAsync(user)).ReturnsAsync(IdentityResult.Success);
        _unitOfWorkMock.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
        _mediatorMock.Setup(x => x.Send(It.Is<GetAccountSettingsQuery>(q => q.UserKey == userId), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ServiceResult<AccountSettingsDto>(ResultType.Success, settings));

        var handler = CreateConfirmHandler();

        // Act
        var result = await handler.Handle(new ConfirmAccountEmailChangeCommand(userId, $" {newEmail} ", token), CancellationToken.None);

        // Assert
        Assert.Equal(ResultType.Success, result.Type);
        Assert.Equal(newEmail, result.Data?.Email);
        Assert.Equal(newEmail, user.Email);
        Assert.Equal(newEmail, user.UserName);
        Assert.Equal("NEW@EXAMPLE.COM", user.NormalizedUserName);
        // A confirmed email change must not leave an older session alive.
        _refreshTokensMock.Verify(
            store => store.RevokeAllAsync(user.Id, It.IsAny<CancellationToken>()),
            Times.Once);
        _memberDirectoryMock.Verify(
            x => x.SetEmailFromAccountAsync(userId, newEmail, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ConfirmAccountEmailChange_ShouldReturnBadRequestAndNotUpdateMember_WhenTokenIsInvalid()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var oldEmail = "old@example.com";
        var newEmail = "new@example.com";
        var user = new AppUser
        {
            Id = userId,
            Email = oldEmail,
            UserName = oldEmail,
            FirstName = "John",
            LastName = "Doe",
        };

        _userManagerMock.Setup(x => x.FindByIdAsync(userId.ToString())).ReturnsAsync(user);
        _userManagerMock.Setup(x => x.FindByEmailAsync(newEmail)).ReturnsAsync((AppUser?)null);
        _userManagerMock.Setup(x => x.ChangeEmailAsync(user, newEmail, "bad-token"))
            .ReturnsAsync(IdentityResult.Failed(new IdentityError { Code = "InvalidToken" }));

        var handler = CreateConfirmHandler();

        // Act
        var result = await handler.Handle(new ConfirmAccountEmailChangeCommand(userId, newEmail, "bad-token"), CancellationToken.None);

        // Assert
        Assert.Equal(ResultType.BadRequest, result.Type);
        Assert.Equal(oldEmail, user.Email);
        Assert.Equal(oldEmail, user.UserName);
        _refreshTokensMock.Verify(
            store => store.RevokeAllAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _memberDirectoryMock.Verify(
            x => x.SetEmailFromAccountAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _unitOfWorkMock.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
        _mediatorMock.Verify(x => x.Send(It.IsAny<GetAccountSettingsQuery>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private UpdateAccountProfileCommandHandler CreateUpdateHandler()
    {
        return new UpdateAccountProfileCommandHandler(
            _userManagerMock.Object,
            _memberDirectoryMock.Object,
            _mediatorMock.Object,
            _emailServiceMock.Object,
            Options.Create(new EmailSettings { BaseUrl = "http://localhost:4200/" }),
            new Mock<IActivityLogger>().Object);
    }

    private ConfirmAccountEmailChangeCommandHandler CreateConfirmHandler()
    {
        return new ConfirmAccountEmailChangeCommandHandler(
            _userManagerMock.Object,
            _memberDirectoryMock.Object,
            _mediatorMock.Object,
            new Mock<IActivityLogger>().Object,
            Revocations.Of(_refreshTokensMock.Object));
    }
}
