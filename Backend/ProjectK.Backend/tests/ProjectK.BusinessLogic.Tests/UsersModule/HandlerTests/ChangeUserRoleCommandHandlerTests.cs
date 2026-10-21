using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Moq;
using ProjectK.BusinessLogic.Modules.UsersModule.Features.User.ChangeRole;
using ProjectK.Common.Entities.AuthModule;
using ProjectK.Common.Interfaces.Modules.InfrastructureModule;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Records;
using Xunit;

namespace ProjectK.BusinessLogic.Tests.UsersModule.HandlerTests;

// ChangeUserRole now only toggles the system Admin role, and only an admin may call it.
// Kurin roles come from діловодські offices (Leadership screen) and are synced, not set here.
public class ChangeUserRoleCommandHandlerTests
{
    private readonly Mock<UserManager<AppUser>> _userManagerMock;
    private readonly Mock<ICurrentUserContext> _currentUserContextMock;
    private readonly Mock<IActivityLogger> _activityLoggerMock;
    private readonly ChangeUserRoleCommandHandler _handler;

    public ChangeUserRoleCommandHandlerTests()
    {
        var store = new Mock<IUserStore<AppUser>>();
        _userManagerMock = new Mock<UserManager<AppUser>>(store.Object, null, null, null, null, null, null, null, null);
        _currentUserContextMock = new Mock<ICurrentUserContext>();
        _activityLoggerMock = new Mock<IActivityLogger>();

        _handler = new ChangeUserRoleCommandHandler(
            _userManagerMock.Object,
            _currentUserContextMock.Object,
            _activityLoggerMock.Object);
    }

    [Fact]
    public async Task Handle_ShouldReturnForbidden_WhenCallerIsNotAdmin()
    {
        var targetUserId = Guid.NewGuid();
        _currentUserContextMock.Setup(c => c.IsInRole("Admin")).Returns(false);
        _userManagerMock.Setup(m => m.FindByIdAsync(targetUserId.ToString()))
            .ReturnsAsync(new AppUser { Id = targetUserId });

        var result = await _handler.Handle(new ChangeUserRoleCommand(targetUserId, UserRole.Admin), CancellationToken.None);

        result.Type.Should().Be(ResultType.Forbidden);
        result.Data.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_ShouldGrantAdmin_WhenAdminPromotesMember()
    {
        var targetUserId = Guid.NewGuid();
        _currentUserContextMock.Setup(c => c.IsInRole("Admin")).Returns(true);
        var user = new AppUser { Id = targetUserId };
        _userManagerMock.Setup(m => m.FindByIdAsync(targetUserId.ToString())).ReturnsAsync(user);
        _userManagerMock.Setup(m => m.GetRolesAsync(user)).ReturnsAsync(new List<string> { "Member" });
        _userManagerMock.Setup(m => m.AddToRoleAsync(user, "Admin")).ReturnsAsync(IdentityResult.Success);

        var result = await _handler.Handle(new ChangeUserRoleCommand(targetUserId, UserRole.Admin), CancellationToken.None);

        result.Type.Should().Be(ResultType.Success);
        result.Data.Should().BeTrue();
        _userManagerMock.Verify(m => m.AddToRoleAsync(user, "Admin"), Times.Once);
    }

    [Fact]
    public async Task Handle_ShouldRevokeAdmin_WhenAdminDemotesToMember()
    {
        var targetUserId = Guid.NewGuid();
        _currentUserContextMock.Setup(c => c.IsInRole("Admin")).Returns(true);
        var user = new AppUser { Id = targetUserId };
        _userManagerMock.Setup(m => m.FindByIdAsync(targetUserId.ToString())).ReturnsAsync(user);
        _userManagerMock.Setup(m => m.GetRolesAsync(user)).ReturnsAsync(new List<string> { "Admin" });
        _userManagerMock.Setup(m => m.RemoveFromRoleAsync(user, "Admin")).ReturnsAsync(IdentityResult.Success);
        _userManagerMock.Setup(m => m.AddToRoleAsync(user, "Member")).ReturnsAsync(IdentityResult.Success);

        var result = await _handler.Handle(new ChangeUserRoleCommand(targetUserId, UserRole.Member), CancellationToken.None);

        result.Type.Should().Be(ResultType.Success);
        result.Data.Should().BeTrue();
        _userManagerMock.Verify(m => m.RemoveFromRoleAsync(user, "Admin"), Times.Once);
    }

    [Fact]
    public async Task Handle_ShouldNoop_WhenTargetAlreadyAdmin()
    {
        var targetUserId = Guid.NewGuid();
        _currentUserContextMock.Setup(c => c.IsInRole("Admin")).Returns(true);
        var user = new AppUser { Id = targetUserId };
        _userManagerMock.Setup(m => m.FindByIdAsync(targetUserId.ToString())).ReturnsAsync(user);
        _userManagerMock.Setup(m => m.GetRolesAsync(user)).ReturnsAsync(new List<string> { "Admin" });

        var result = await _handler.Handle(new ChangeUserRoleCommand(targetUserId, UserRole.Admin), CancellationToken.None);

        result.Type.Should().Be(ResultType.Success);
        _userManagerMock.Verify(m => m.AddToRoleAsync(It.IsAny<AppUser>(), It.IsAny<string>()), Times.Never);
    }
}
