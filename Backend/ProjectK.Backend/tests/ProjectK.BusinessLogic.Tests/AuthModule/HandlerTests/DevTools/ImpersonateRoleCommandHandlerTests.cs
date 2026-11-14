using Microsoft.AspNetCore.Identity;
using Moq;
using ProjectK.BusinessLogic.Modules.AuthModule.Features.DevTools;
using ProjectK.BusinessLogic.Modules.AuthModule.Services;
using ProjectK.BusinessLogic.Tests.TestHelpers;
using ProjectK.Common.Contracts.AuthModule;
using ProjectK.Common.Contracts.InfrastructureModule;
using ProjectK.Common.Contracts.KurinModule;
using ProjectK.Common.Contracts.MemberModule;
using ProjectK.Common.Contracts.Shared;
using ProjectK.Common.Entities.AuthModule;
using ProjectK.Common.Entities.KurinModule;
using ProjectK.Common.Enums.AuthModule;
using ProjectK.Common.Models.Authorization;
using ProjectK.Common.Models.Dtos.AuthModule;
using ProjectK.Common.Models.Enums;

namespace ProjectK.BusinessLogic.Tests.AuthModule.HandlerTests.DevTools;

/// <summary>
/// The dev role switcher: an administrator borrows the seat of whoever holds an office in the
/// kurin on screen, and gets a ticket back to their own account.
/// </summary>
public class ImpersonateRoleCommandHandlerTests
{
    private readonly Mock<UserManager<AppUser>> _userManager;
    private readonly Mock<IKurinRepository> _kurins = new();
    private readonly Mock<ILeadershipRepository> _leaderships = new();
    private readonly Mock<IMemberDirectory> _members = new();
    private readonly Mock<IMembershipDirectory> _memberships = new();
    private readonly Mock<ILoginResponseFactory> _loginResponses = new();
    private readonly Mock<IJwtService> _jwt = new();
    private readonly Mock<ICurrentUserContext> _currentUser = new();
    private readonly Mock<IActivityLogger> _activityLogger = new();
    private readonly Mock<IRefreshTokenStore> _refreshTokens = new();
    private readonly Guid _adminKey = Guid.NewGuid();
    private readonly Guid _kurinKey = Guid.NewGuid();

    public ImpersonateRoleCommandHandlerTests()
    {
        _userManager = new Mock<UserManager<AppUser>>(new Mock<IUserStore<AppUser>>().Object, null, null, null, null, null, null, null, null);
        _userManager.Setup(m => m.UpdateAsync(It.IsAny<AppUser>())).ReturnsAsync(IdentityResult.Success);
        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork.Setup(u => u.Kurins).Returns(_kurins.Object);
        unitOfWork.Setup(u => u.Leaderships).Returns(_leaderships.Object);
        _unitOfWork = unitOfWork.Object;

        _currentUser.SetupGet(c => c.UserId).Returns(_adminKey);
        _currentUser.SetupGet(c => c.KurinKey).Returns(_kurinKey);
        _currentUser.Setup(c => c.IsInRole(SystemRole.Admin)).Returns(true);
        _jwt.Setup(j => j.GenerateDevReturnTicket(_adminKey)).Returns("ticket");
        _loginResponses
            .Setup(f => f.CreateAsync(It.IsAny<AppUser>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((AppUser user, CancellationToken _) => Logins.Empty with { UserKey = user.Id, Email = user.Email! });
    }

    private readonly IUnitOfWork _unitOfWork;

    private ImpersonateRoleCommandHandler Handler() => new(
        _userManager.Object, _unitOfWork, _members.Object, _memberships.Object,
        _loginResponses.Object, _jwt.Object, _currentUser.Object, _activityLogger.Object);

    private AppUser Account(string email)
    {
        var user = new AppUser { Id = Guid.NewGuid(), Email = email, OnboardingStatus = OnboardingStatus.Active };
        _userManager.Setup(m => m.FindByIdAsync(user.Id.ToString())).ReturnsAsync(user);
        _userManager.Setup(m => m.IsInRoleAsync(user, SystemRole.Admin)).ReturnsAsync(false);
        return user;
    }

    private void Holds(LeadershipRole role, Guid memberKey, AppUser? account)
    {
        _leaderships
            .Setup(r => r.GetActiveOfficeMemberKeysAsync(
                It.Is<IReadOnlyCollection<LeadershipRole>>(roles => roles.Contains(role)), _kurinKey, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync([memberKey]);
        _members.Setup(d => d.FindAccountKeyAsync(memberKey, It.IsAny<CancellationToken>())).ReturnsAsync(account?.Id);
    }

    [Fact]
    public async Task Handle_ShouldSignInAsTheOfficeHolder_InTheKurinOnScreen_AndHandBackATicket()
    {
        var zvyazkovyi = Account("zv@example.com");
        Holds(LeadershipRole.Zvyazkovyi, Guid.NewGuid(), zvyazkovyi);

        var result = await Handler().Handle(new ImpersonateRoleCommand(DevRole.Zvyazkovyi, null), CancellationToken.None);

        Assert.Equal(ResultType.Success, result.Type);
        Assert.Equal(zvyazkovyi.Id, result.Data!.Login.UserKey);
        Assert.Equal("ticket", result.Data.ReturnTicket);
        Assert.Equal(_kurinKey, result.Data.KurinKey);
        Assert.Equal(_kurinKey, zvyazkovyi.ActiveKurinKey);
        _activityLogger.Verify(l => l.LogAudit("Dev.Impersonate", _adminKey, zvyazkovyi.Id, null, null, It.IsAny<string>()), Times.Once);
    }

    /// <summary>
    /// A гуртковий is still a youth: only kurin-wide and КВ offices take somebody out of the running,
    /// because in demo data nearly everyone holds some гурток office.
    /// </summary>
    [Fact]
    public async Task Handle_ShouldPickAMemberWithNoKurinOrKvOffice_ForThePlainMemberRole()
    {
        var vykhovnyk = Account("v@example.com");
        var kurinnyi = Account("k@example.com");
        var hurtkovyi = Account("h@example.com");
        _memberships
            .Setup(d => d.GetAccountKeysInKurinAsync(_kurinKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync([vykhovnyk.Id, kurinnyi.Id, hurtkovyi.Id]);
        _leaderships
            .Setup(r => r.GetActiveOfficesForAccountInKurinAsync(vykhovnyk.Id, _kurinKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync([new MemberOffice(LeadershipType.KV, LeadershipRole.Vykhovnyk)]);
        _leaderships
            .Setup(r => r.GetActiveOfficesForAccountInKurinAsync(kurinnyi.Id, _kurinKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync([new MemberOffice(LeadershipType.Kurin, LeadershipRole.Kurinnuy)]);
        _leaderships
            .Setup(r => r.GetActiveOfficesForAccountInKurinAsync(hurtkovyi.Id, _kurinKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync([new MemberOffice(LeadershipType.Group, LeadershipRole.Hurtkoviy)]);

        var result = await Handler().Handle(new ImpersonateRoleCommand(DevRole.Member, null), CancellationToken.None);

        Assert.Equal(ResultType.Success, result.Type);
        Assert.Equal(hurtkovyi.Id, result.Data!.Login.UserKey);
    }

    [Fact]
    public async Task Handle_ShouldSayWhenNobodyHoldsTheRole_WithAnAccount()
    {
        Holds(LeadershipRole.Skarbnyk, Guid.NewGuid(), account: null);

        var result = await Handler().Handle(new ImpersonateRoleCommand(DevRole.Skarbnyk, null), CancellationToken.None);

        Assert.Equal(ResultType.NotFound, result.Type);
        Assert.Equal("NoAccountForRole", result.ErrorCode);
    }

    [Fact]
    public async Task Handle_ShouldFallBackToTheDemoKurin_WhenTheAdminStandsNowhere()
    {
        _currentUser.SetupGet(c => c.KurinKey).Returns((Guid?)null);
        var demo = new Kurin(1) { KurinKey = Guid.NewGuid() };
        _kurins.Setup(r => r.GetByNumberAsync("ua", 1, It.IsAny<CancellationToken>())).ReturnsAsync(demo);
        var holder = Account("k@example.com");
        _leaderships
            .Setup(r => r.GetActiveOfficeMemberKeysAsync(It.IsAny<IReadOnlyCollection<LeadershipRole>>(), demo.KurinKey, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync([Guid.NewGuid()]);
        _members.Setup(d => d.FindAccountKeyAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(holder.Id);

        var result = await Handler().Handle(new ImpersonateRoleCommand(DevRole.Kurinnyi, null), CancellationToken.None);

        Assert.Equal(ResultType.Success, result.Type);
        Assert.Equal(demo.KurinKey, result.Data!.KurinKey);
    }

    [Fact]
    public async Task Handle_ShouldRefuseAnyoneWhoIsNotAnAdministrator()
    {
        _currentUser.Setup(c => c.IsInRole(SystemRole.Admin)).Returns(false);

        var result = await Handler().Handle(new ImpersonateRoleCommand(DevRole.Zvyazkovyi, null), CancellationToken.None);

        Assert.Equal(ResultType.Forbidden, result.Type);
    }

    [Fact]
    public async Task Return_ShouldEndTheBorrowedSession_AndSignTheAdministratorBackIn()
    {
        var admin = new AppUser { Id = _adminKey, Email = "admin@example.com" };
        _userManager.Setup(m => m.FindByIdAsync(_adminKey.ToString())).ReturnsAsync(admin);
        _userManager.Setup(m => m.IsInRoleAsync(admin, SystemRole.Admin)).ReturnsAsync(true);
        _jwt.Setup(j => j.ReadDevReturnTicket("ticket")).Returns(_adminKey);
        var handler = new ReturnFromImpersonationCommandHandler(
            _userManager.Object, _jwt.Object, _refreshTokens.Object, _loginResponses.Object, _activityLogger.Object);

        var result = await handler.Handle(new ReturnFromImpersonationCommand("ticket", ["borrowed-session"]), CancellationToken.None);

        Assert.Equal(ResultType.Success, result.Type);
        Assert.Equal(_adminKey, result.Data!.UserKey);
        _refreshTokens.Verify(s => s.RevokeAsync("borrowed-session", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("forged")]
    public async Task Return_ShouldRefuseWithoutAValidTicket(string? ticket)
    {
        _jwt.Setup(j => j.ReadDevReturnTicket(It.IsAny<string>())).Returns((Guid?)null);
        var handler = new ReturnFromImpersonationCommandHandler(
            _userManager.Object, _jwt.Object, _refreshTokens.Object, _loginResponses.Object, _activityLogger.Object);

        var result = await handler.Handle(new ReturnFromImpersonationCommand(ticket!, []), CancellationToken.None);

        Assert.Equal(ResultType.Unauthorized, result.Type);
        _refreshTokens.Verify(s => s.RevokeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
