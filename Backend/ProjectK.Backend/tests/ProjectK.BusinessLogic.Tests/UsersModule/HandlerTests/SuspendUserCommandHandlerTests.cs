using Microsoft.AspNetCore.Identity;
using Moq;
using ProjectK.BusinessLogic.Tests.TestHelpers;
using ProjectK.BusinessLogic.Modules.UsersModule.Features.User.Suspend;
using ProjectK.Common.Entities.AuthModule;
using ProjectK.Common.Interfaces.Modules.AuthModule;
using ProjectK.Common.Interfaces.Modules.InfrastructureModule;
using ProjectK.Common.Models.Enums;

namespace ProjectK.BusinessLogic.Tests.UsersModule.HandlerTests;

/// <summary>
/// SEC-4.3: the reversible alternative to deleting an account. Suspending refuses sign-in and
/// ends every session; restoring lets the person sign in again.
/// </summary>
public class SuspendUserCommandHandlerTests
{
    private readonly Mock<UserManager<AppUser>> _userManager;
    private readonly Mock<ICurrentUserContext> _currentUser = new();
    private readonly Mock<IRefreshTokenStore> _refreshTokens = new();
    private readonly Mock<IActivityLogger> _activityLogger = new();
    private readonly Guid _adminKey = Guid.NewGuid();

    public SuspendUserCommandHandlerTests()
    {
        _userManager = new Mock<UserManager<AppUser>>(
            new Mock<IUserStore<AppUser>>().Object, null, null, null, null, null, null, null, null);
        _currentUser.SetupGet(c => c.UserId).Returns(_adminKey);
        _userManager.Setup(m => m.UpdateAsync(It.IsAny<AppUser>())).ReturnsAsync(IdentityResult.Success);
    }

    private SuspendUserCommandHandler Suspend() =>
        new(_userManager.Object, _currentUser.Object, Revocations.Of(_refreshTokens.Object), _activityLogger.Object);

    private RestoreUserCommandHandler Restore() =>
        new(_userManager.Object, _currentUser.Object, _activityLogger.Object);

    private AppUser Account(OnboardingStatus status = OnboardingStatus.Active)
    {
        var user = new AppUser { Id = Guid.NewGuid(), Email = "person@example.com", OnboardingStatus = status };
        _userManager.Setup(m => m.FindByIdAsync(user.Id.ToString())).ReturnsAsync(user);
        return user;
    }

    [Fact]
    public async Task Suspend_ShouldRefuseSignIn_EndEverySession_AndSayWhoDidIt()
    {
        var target = Account();
        var order = new List<string>();
        _userManager.Setup(m => m.UpdateAsync(target)).Callback(() => order.Add("update")).ReturnsAsync(IdentityResult.Success);
        _refreshTokens.Setup(s => s.RevokeAllAsync(target.Id, It.IsAny<CancellationToken>())).Callback(() => order.Add("revoke")).Returns(Task.CompletedTask);

        var result = await Suspend().Handle(new SuspendUserCommand(target.Id), CancellationToken.None);

        Assert.Equal(ResultType.Success, result.Type);
        Assert.Equal(OnboardingStatus.Suspended, target.OnboardingStatus);
        // Stored first, then the sessions go: a revoke before a failed update would only have signed them out once.
        Assert.Equal(["update", "revoke"], order);
        _activityLogger.Verify(l => l.LogAudit("Admin.UserSuspended", _adminKey, target.Id, null, null, It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task Suspend_ShouldRefuseTheAdministratorsOwnAccount()
    {
        var self = Account();
        self.Id = _adminKey;
        _userManager.Setup(m => m.FindByIdAsync(_adminKey.ToString())).ReturnsAsync(self);

        var result = await Suspend().Handle(new SuspendUserCommand(_adminKey), CancellationToken.None);

        Assert.Equal(ResultType.BadRequest, result.Type);
        Assert.Equal("CannotSuspendSelf", result.ErrorCode);
        Assert.Equal(OnboardingStatus.Active, self.OnboardingStatus);
        _refreshTokens.Verify(s => s.RevokeAllAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Suspend_ShouldNotEndSessions_WhenTheStatusCouldNotBeStored()
    {
        var target = Account();
        _userManager.Setup(m => m.UpdateAsync(target)).ReturnsAsync(IdentityResult.Failed());

        var result = await Suspend().Handle(new SuspendUserCommand(target.Id), CancellationToken.None);

        Assert.Equal(ResultType.BadRequest, result.Type);
        _refreshTokens.Verify(s => s.RevokeAllAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Suspend_ShouldAnswerNotFound_ForAnAccountThatIsGone()
    {
        var missing = Guid.NewGuid();
        _userManager.Setup(m => m.FindByIdAsync(missing.ToString())).ReturnsAsync((AppUser?)null);

        var result = await Suspend().Handle(new SuspendUserCommand(missing), CancellationToken.None);

        Assert.Equal(ResultType.NotFound, result.Type);
    }

    [Theory]
    [InlineData(OnboardingStatus.Suspended)]
    [InlineData(OnboardingStatus.Archived)]
    public async Task Restore_ShouldMakeTheAccountActiveAgain(OnboardingStatus from)
    {
        var target = Account(from);

        var result = await Restore().Handle(new RestoreUserCommand(target.Id), CancellationToken.None);

        Assert.Equal(ResultType.Success, result.Type);
        Assert.Equal(OnboardingStatus.Active, target.OnboardingStatus);
        _activityLogger.Verify(l => l.LogAudit("Admin.UserRestored", _adminKey, target.Id, null, null, It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task Restore_ShouldLeaveAnActiveAccountAlone()
    {
        var target = Account();

        var result = await Restore().Handle(new RestoreUserCommand(target.Id), CancellationToken.None);

        Assert.Equal(ResultType.Success, result.Type);
        _userManager.Verify(m => m.UpdateAsync(It.IsAny<AppUser>()), Times.Never);
    }
}
