using Microsoft.AspNetCore.Identity;
using Moq;
using ProjectK.BusinessLogic.Modules.AuthModule.Features.DevTools;
using ProjectK.BusinessLogic.Modules.AuthModule.Services;
using ProjectK.BusinessLogic.Tests.TestHelpers;
using ProjectK.Common.Entities.AuthModule;
using ProjectK.Common.Interfaces.Modules.InfrastructureModule;
using ProjectK.Common.Interfaces.Modules.KurinModule;
using ProjectK.Common.Interfaces.Modules.MemberModule;
using ProjectK.Common.Models.Authorization;
using ProjectK.Common.Models.Dtos.AuthModule;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Records;

namespace ProjectK.BusinessLogic.Tests.AuthModule.HandlerTests.DevTools;

/// <summary>
/// The "this person" seat of the dev role switcher: an administrator with somebody's card open
/// steps into that somebody's account.
/// </summary>
public class ImpersonateMemberCommandHandlerTests
{
    private readonly Mock<UserManager<AppUser>> _userManager;
    private readonly Mock<IMemberDirectory> _members = new();
    private readonly Mock<IMembershipDirectory> _memberships = new();
    private readonly Mock<ILoginResponseFactory> _loginResponses = new();
    private readonly Mock<IJwtService> _jwt = new();
    private readonly Mock<ICurrentUserContext> _currentUser = new();
    private readonly Mock<IActivityLogger> _activityLogger = new();
    private readonly Guid _adminKey = Guid.NewGuid();
    private readonly Guid _onScreenKurin = Guid.NewGuid();

    public ImpersonateMemberCommandHandlerTests()
    {
        _userManager = new Mock<UserManager<AppUser>>(new Mock<IUserStore<AppUser>>().Object, null, null, null, null, null, null, null, null);
        _userManager.Setup(m => m.UpdateAsync(It.IsAny<AppUser>())).ReturnsAsync(IdentityResult.Success);
        _currentUser.SetupGet(c => c.UserId).Returns(_adminKey);
        _currentUser.SetupGet(c => c.KurinKey).Returns(_onScreenKurin);
        _currentUser.Setup(c => c.IsInRole(SystemRole.Admin)).Returns(true);
        _jwt.Setup(j => j.GenerateDevReturnTicket(_adminKey)).Returns("ticket");
        _loginResponses
            .Setup(f => f.CreateAsync(It.IsAny<AppUser>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((AppUser user, CancellationToken _) => Logins.Empty with { UserKey = user.Id, Email = user.Email! });
    }

    private ImpersonateMemberCommandHandler Handler() => new(
        _userManager.Object, _members.Object, _memberships.Object,
        _loginResponses.Object, _jwt.Object, _currentUser.Object, _activityLogger.Object);

    private AppUser Account(string email, bool admin = false)
    {
        var user = new AppUser { Id = Guid.NewGuid(), Email = email, OnboardingStatus = OnboardingStatus.Active };
        _userManager.Setup(m => m.FindByIdAsync(user.Id.ToString())).ReturnsAsync(user);
        _userManager.Setup(m => m.IsInRoleAsync(user, SystemRole.Admin)).ReturnsAsync(admin);
        return user;
    }

    private MemberSummary Person(AppUser? account, Guid ownKurin, params Guid[] standsIn)
    {
        var summary = new MemberSummary(Guid.NewGuid(), account?.Id, ownKurin, null, "Марта", "Сова", "m@example.com", null);
        _members.Setup(d => d.FindAsync(summary.MemberKey, It.IsAny<CancellationToken>())).ReturnsAsync(summary);
        if (account is not null)
        {
            _memberships.Setup(d => d.GetKurinKeysForAccountAsync(account.Id, It.IsAny<CancellationToken>())).ReturnsAsync(standsIn);
        }

        return summary;
    }

    [Fact]
    public async Task Handle_ShouldSignInAsThePerson_InTheKurinOnScreen_WhenTheyStandThere()
    {
        var account = Account("m@example.com");
        var person = Person(account, ownKurin: Guid.NewGuid(), Guid.NewGuid(), _onScreenKurin);

        var result = await Handler().Handle(new ImpersonateMemberCommand(person.MemberKey), CancellationToken.None);

        Assert.Equal(ResultType.Success, result.Type);
        Assert.Equal(account.Id, result.Data!.Login.UserKey);
        Assert.Equal("ticket", result.Data.ReturnTicket);
        Assert.Equal(ImpersonateMemberCommandHandler.PersonRole, result.Data.Role);
        Assert.Equal(_onScreenKurin, result.Data.KurinKey);
        Assert.Equal(_onScreenKurin, account.ActiveKurinKey);
        _activityLogger.Verify(l => l.LogAudit("Dev.Impersonate", _adminKey, account.Id, null, null, It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ShouldLandInThePersonsOwnKurin_WhenTheyDoNotStandInTheOneOnScreen()
    {
        var account = Account("m@example.com");
        var elsewhere = Guid.NewGuid();
        var person = Person(account, ownKurin: elsewhere, elsewhere);

        var result = await Handler().Handle(new ImpersonateMemberCommand(person.MemberKey), CancellationToken.None);

        Assert.Equal(ResultType.Success, result.Type);
        Assert.Equal(elsewhere, result.Data!.KurinKey);
        Assert.Equal(elsewhere, account.ActiveKurinKey);
    }

    [Fact]
    public async Task Handle_ShouldSayWhenThePersonHasNoAccount()
    {
        var person = Person(account: null, ownKurin: _onScreenKurin);

        var result = await Handler().Handle(new ImpersonateMemberCommand(person.MemberKey), CancellationToken.None);

        Assert.Equal(ResultType.NotFound, result.Type);
        Assert.Equal("NoAccountForMember", result.ErrorCode);
    }

    [Fact]
    public async Task Handle_ShouldSayWhenThereIsNoSuchMember()
    {
        _members.Setup(d => d.FindAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync((MemberSummary?)null);

        var result = await Handler().Handle(new ImpersonateMemberCommand(Guid.NewGuid()), CancellationToken.None);

        Assert.Equal(ResultType.NotFound, result.Type);
        Assert.Equal("NoSuchMember", result.ErrorCode);
    }

    [Fact]
    public async Task Handle_ShouldNotBorrowAnotherAdministratorsSeat()
    {
        var other = Account("root@example.com", admin: true);
        var person = Person(other, ownKurin: _onScreenKurin, _onScreenKurin);

        var result = await Handler().Handle(new ImpersonateMemberCommand(person.MemberKey), CancellationToken.None);

        Assert.Equal(ResultType.Conflict, result.Type);
        Assert.Equal("AdminAccount", result.ErrorCode);
        _loginResponses.Verify(f => f.CreateAsync(It.IsAny<AppUser>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ShouldRefuseAnyoneWhoIsNotAnAdministrator()
    {
        _currentUser.Setup(c => c.IsInRole(SystemRole.Admin)).Returns(false);

        var result = await Handler().Handle(new ImpersonateMemberCommand(Guid.NewGuid()), CancellationToken.None);

        Assert.Equal(ResultType.Forbidden, result.Type);
        _members.Verify(d => d.FindAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
