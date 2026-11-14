using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Hosting;
using Moq;
using ProjectK.BusinessLogic.Modules.AuthModule.Features.Demo;
using ProjectK.BusinessLogic.Modules.AuthModule.Models;
using ProjectK.BusinessLogic.Modules.AuthModule.Services;
using ProjectK.Common.Entities.AuthModule;
using ProjectK.Common.Entities.KurinModule;
using ProjectK.Common.Interfaces;
using ProjectK.Common.Interfaces.Modules.InfrastructureModule;
using ProjectK.Common.Interfaces.Modules.KurinModule;
using ProjectK.Common.Interfaces.Modules.MemberModule;
using ProjectK.Common.Models.Authorization;
using ProjectK.Common.Models.Enums;

namespace ProjectK.BusinessLogic.Tests.AuthModule.HandlerTests.Demo;

/// <summary>
/// The public demo's front door: a seat in kurin 1, no password, and only where the tier says Demo.
/// </summary>
public class EnterDemoCommandHandlerTests
{
    private readonly Mock<IHostEnvironment> _environment = new();
    private readonly Mock<UserManager<AppUser>> _userManager;
    private readonly Mock<IKurinRepository> _kurins = new();
    private readonly Mock<ILeadershipRepository> _leaderships = new();
    private readonly Mock<IMemberDirectory> _members = new();
    private readonly Mock<IMembershipDirectory> _memberships = new();
    private readonly Mock<ILoginResponseFactory> _loginResponses = new();
    private readonly Mock<IActivityLogger> _activityLogger = new();
    private readonly IUnitOfWork _unitOfWork;
    private readonly Kurin _demoKurin = new(1) { KurinKey = Guid.NewGuid() };

    public EnterDemoCommandHandlerTests()
    {
        _environment.SetupGet(e => e.EnvironmentName).Returns("Demo");
        _userManager = new Mock<UserManager<AppUser>>(new Mock<IUserStore<AppUser>>().Object, null, null, null, null, null, null, null, null);
        _userManager.Setup(m => m.UpdateAsync(It.IsAny<AppUser>())).ReturnsAsync(IdentityResult.Success);
        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork.Setup(u => u.Kurins).Returns(_kurins.Object);
        unitOfWork.Setup(u => u.Leaderships).Returns(_leaderships.Object);
        _unitOfWork = unitOfWork.Object;
        _kurins.Setup(k => k.GetByNumberAsync("ua", 1, It.IsAny<CancellationToken>())).ReturnsAsync(_demoKurin);
        _loginResponses
            .Setup(f => f.CreateAsync(It.IsAny<AppUser>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((AppUser user, CancellationToken _) => new LoginUserResponse { UserKey = user.Id, Email = user.Email! });
    }

    private EnterDemoCommandHandler Handler() => new(
        _environment.Object, _userManager.Object, _unitOfWork, _members.Object, _memberships.Object,
        _loginResponses.Object, _activityLogger.Object);

    private AppUser Account(string email)
    {
        var user = new AppUser { Id = Guid.NewGuid(), Email = email, OnboardingStatus = OnboardingStatus.Active };
        _userManager.Setup(m => m.FindByIdAsync(user.Id.ToString())).ReturnsAsync(user);
        _userManager.Setup(m => m.IsInRoleAsync(user, SystemRole.Admin)).ReturnsAsync(false);
        return user;
    }

    private void Holds(LeadershipRole role, AppUser account)
    {
        var memberKey = Guid.NewGuid();
        _leaderships
            .Setup(r => r.GetActiveOfficeMemberKeysAsync(
                It.Is<IReadOnlyCollection<LeadershipRole>>(roles => roles.Contains(role)), _demoKurin.KurinKey, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync([memberKey]);
        _members.Setup(d => d.FindAccountKeyAsync(memberKey, It.IsAny<CancellationToken>())).ReturnsAsync(account.Id);
    }

    [Fact]
    public async Task Handle_ShouldSignInAsTheSeatHolder_InTheDemoKurin()
    {
        var zvyazkovyi = Account("demo0@example.com");
        Holds(LeadershipRole.Zvyazkovyi, zvyazkovyi);

        var result = await Handler().Handle(new EnterDemoCommand(DemoSeat.Zvyazkovyi), CancellationToken.None);

        Assert.Equal(ResultType.Success, result.Type);
        Assert.Equal(zvyazkovyi.Id, result.Data!.UserKey);
        Assert.Equal(_demoKurin.KurinKey, zvyazkovyi.ActiveKurinKey);
        _activityLogger.Verify(l => l.LogAudit("Demo.Enter", null, zvyazkovyi.Id, null, null, It.IsAny<string>()), Times.Once);
    }

    // The controller is absent outside Demo; the handler is the second lock on the same door.
    [Fact]
    public async Task Handle_ShouldRefuse_OutsideTheDemoTier()
    {
        _environment.SetupGet(e => e.EnvironmentName).Returns("Production");
        Holds(LeadershipRole.Zvyazkovyi, Account("demo0@example.com"));

        var result = await Handler().Handle(new EnterDemoCommand(DemoSeat.Zvyazkovyi), CancellationToken.None);

        Assert.Equal(ResultType.NotFound, result.Type);
        Assert.Equal("NotDemo", result.ErrorCode);
        _loginResponses.Verify(f => f.CreateAsync(It.IsAny<AppUser>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ShouldSayWhenTheSeatIsEmpty()
    {
        _leaderships
            .Setup(r => r.GetActiveOfficeMemberKeysAsync(It.IsAny<IReadOnlyCollection<LeadershipRole>>(), _demoKurin.KurinKey, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var result = await Handler().Handle(new EnterDemoCommand(DemoSeat.Vykhovnyk), CancellationToken.None);

        Assert.Equal(ResultType.NotFound, result.Type);
        Assert.Equal("NoAccountForSeat", result.ErrorCode);
    }
}
