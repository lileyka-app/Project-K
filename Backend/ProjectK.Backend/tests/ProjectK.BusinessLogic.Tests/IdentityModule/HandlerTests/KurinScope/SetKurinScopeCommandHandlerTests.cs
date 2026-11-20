using Microsoft.AspNetCore.Identity;
using Moq;
using ProjectK.BusinessLogic.Modules.IdentityModule.Features.KurinScope;
using ProjectK.BusinessLogic.Modules.IdentityModule.Services;
using ProjectK.BusinessLogic.Tests.TestHelpers;
using ProjectK.Common.Contracts.KurinModule;
using ProjectK.Common.Contracts.MemberModule;
using ProjectK.Common.Contracts.Shared;
using ProjectK.Common.Entities.IdentityModule;
using ProjectK.Common.Entities.KurinModule;
using ProjectK.Common.Entities.MemberModule;
using ProjectK.Common.Enums.Shared;
using ProjectK.Common.Rules.IdentityModule;

namespace ProjectK.BusinessLogic.Tests.IdentityModule.HandlerTests.KurinScope;

public class SetKurinScopeCommandHandlerTests
{
    private readonly Mock<UserManager<AppUser>> _userManagerMock;
    private readonly Mock<IKurinRepository> _kurinsMock = new();
    private readonly Mock<ILoginResponseService> _loginResponseFactoryMock = new();
    private readonly Mock<IMembershipDirectory> _membershipsMock = new();
    private readonly SetKurinScopeCommandHandler _handler;

    public SetKurinScopeCommandHandlerTests()
    {
        var userStoreMock = new Mock<IUserStore<AppUser>>();
        _userManagerMock = new Mock<UserManager<AppUser>>(
            userStoreMock.Object, null, null, null, null, null, null, null, null);

        var unitOfWorkMock = new Mock<IUnitOfWork>();
        unitOfWorkMock.SetupGet(x => x.Kurins).Returns(_kurinsMock.Object);

        _loginResponseFactoryMock
            .Setup(x => x.CreateAsync(It.IsAny<AppUser>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Logins.Empty with { Email = "admin@projectk.com", IsAdmin = true });

        _handler = new SetKurinScopeCommandHandler(
            _userManagerMock.Object,
            unitOfWorkMock.Object,
            _loginResponseFactoryMock.Object,
            _membershipsMock.Object);
    }

    private AppUser ArrangeUser(params string[] roles)
    {
        var user = new AppUser
        {
            Id = Guid.NewGuid(),
            Email = "admin@projectk.com",
            FirstName = "System",
            LastName = "Admin"
        };

        _userManagerMock.Setup(x => x.FindByIdAsync(user.Id.ToString())).ReturnsAsync(user);
        _userManagerMock.Setup(x => x.IsInRoleAsync(user, SystemRoleNames.Admin))
            .ReturnsAsync(roles.Contains(SystemRoleNames.Admin));
        _userManagerMock.Setup(x => x.UpdateAsync(user)).ReturnsAsync(IdentityResult.Success);

        return user;
    }

    [Fact]
    public async Task Handle_ShouldScopeAdminIntoKurin()
    {
        var user = ArrangeUser("Admin");
        var kurinKey = Guid.NewGuid();
        _kurinsMock.Setup(x => x.GetByKeyAsync(kurinKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Kurin(12) { KurinKey = kurinKey });

        var result = await _handler.Handle(new SetKurinScopeCommand(user.Id, kurinKey), CancellationToken.None);

        Assert.Equal(ResultType.Success, result.Type);
        Assert.Equal(kurinKey, user.ActiveKurinKey);
        _loginResponseFactoryMock.Verify(x => x.CreateAsync(user, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ShouldClearScope_WhenKurinKeyIsNull()
    {
        var user = ArrangeUser("Admin");
        user.ActiveKurinKey = Guid.NewGuid();

        var result = await _handler.Handle(new SetKurinScopeCommand(user.Id, null), CancellationToken.None);

        Assert.Equal(ResultType.Success, result.Type);
        Assert.Null(user.ActiveKurinKey);
    }

    [Fact]
    public async Task Handle_ShouldLetAnyoneIntoAKurinTheyBelongTo()
    {
        var user = ArrangeUser(SystemRoleNames.Member);
        var kurinKey = Guid.NewGuid();
        _kurinsMock.Setup(x => x.GetByKeyAsync(kurinKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Kurin(12) { KurinKey = kurinKey });
        _membershipsMock
            .Setup(x => x.GetKurinKeysForAccountAsync(user.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync([kurinKey]);

        var result = await _handler.Handle(new SetKurinScopeCommand(user.Id, kurinKey), CancellationToken.None);

        Assert.Equal(ResultType.Success, result.Type);
        Assert.Equal(kurinKey, user.ActiveKurinKey);
    }

    [Fact]
    public async Task Handle_ShouldForbid_AKurinTheyDoNotBelongTo()
    {
        var user = ArrangeUser(SystemRoleNames.Member);
        var theirKurin = Guid.NewGuid();
        var someoneElses = Guid.NewGuid();
        _kurinsMock.Setup(x => x.GetByKeyAsync(someoneElses, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Kurin(12) { KurinKey = someoneElses });
        _membershipsMock
            .Setup(x => x.GetKurinKeysForAccountAsync(user.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync([theirKurin]);

        var result = await _handler.Handle(new SetKurinScopeCommand(user.Id, someoneElses), CancellationToken.None);

        Assert.Equal(ResultType.Forbidden, result.Type);
        Assert.Equal("KurinScopeForbidden", result.ErrorCode);
        Assert.Null(user.ActiveKurinKey);
        _userManagerMock.Verify(x => x.UpdateAsync(It.IsAny<AppUser>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ShouldForbidStandingOutsideEveryKurin_UnlessAdmin()
    {
        var user = ArrangeUser(SystemRoleNames.Member);

        var result = await _handler.Handle(new SetKurinScopeCommand(user.Id, null), CancellationToken.None);

        Assert.Equal(ResultType.Forbidden, result.Type);
        Assert.Equal("KurinScopeForbidden", result.ErrorCode);
        _userManagerMock.Verify(x => x.UpdateAsync(It.IsAny<AppUser>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ShouldReturnNotFound_WhenKurinDoesNotExist()
    {
        var user = ArrangeUser("Admin");
        var kurinKey = Guid.NewGuid();
        _kurinsMock.Setup(x => x.GetByKeyAsync(kurinKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Kurin?)null);

        var result = await _handler.Handle(new SetKurinScopeCommand(user.Id, kurinKey), CancellationToken.None);

        Assert.Equal(ResultType.NotFound, result.Type);
        Assert.Equal("KurinNotFound", result.ErrorCode);
        Assert.Null(user.ActiveKurinKey);
    }

    [Fact]
    public async Task Handle_ShouldReturnUnauthorized_WhenUserIsMissing()
    {
        var missingKey = Guid.NewGuid();
        _userManagerMock.Setup(x => x.FindByIdAsync(missingKey.ToString())).ReturnsAsync((AppUser?)null);

        var result = await _handler.Handle(new SetKurinScopeCommand(missingKey, null), CancellationToken.None);

        Assert.Equal(ResultType.Unauthorized, result.Type);
    }
}
