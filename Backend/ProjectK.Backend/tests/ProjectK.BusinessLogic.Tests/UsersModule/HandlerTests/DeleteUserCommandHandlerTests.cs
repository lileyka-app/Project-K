using Microsoft.AspNetCore.Identity;
using Moq;
using ProjectK.BusinessLogic.Modules.UsersModule.Features.User;
using ProjectK.Common.Contracts.MemberModule;
using ProjectK.Common.Contracts.Shared;
using ProjectK.Common.Entities.AuthModule;
using ProjectK.Common.Entities.MemberModule;
using ProjectK.Common.Enums.Shared;

namespace ProjectK.BusinessLogic.Tests.UsersModule.HandlerTests;

public class DeleteUserCommandHandlerTests
{
    private readonly Mock<UserManager<AppUser>> _userManagerMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly Mock<IMentorAssignmentRepository> _mentorAssignmentRepositoryMock;
    private readonly DeleteUserCommandHandler _handler;

    public DeleteUserCommandHandlerTests()
    {
        var userStoreMock = new Mock<IUserStore<AppUser>>();
        _userManagerMock = new Mock<UserManager<AppUser>>(
            userStoreMock.Object, null, null, null, null, null, null, null, null);

        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _mentorAssignmentRepositoryMock = new Mock<IMentorAssignmentRepository>();
        _unitOfWorkMock.Setup(u => u.MentorAssignments).Returns(_mentorAssignmentRepositoryMock.Object);

        _handler = new DeleteUserCommandHandler(_userManagerMock.Object, _unitOfWorkMock.Object);
    }

    [Fact]
    public async Task Handle_ShouldReturnNotFound_WhenUserDoesNotExist()
    {
        // Arrange
        var userId = Guid.NewGuid();
        _userManagerMock.Setup(x => x.FindByIdAsync(userId.ToString()))
            .ReturnsAsync((AppUser)null);

        var command = new DeleteUserCommand(userId);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.Equal(ResultType.NotFound, result.Type);
    }

    [Fact]
    public async Task Handle_ShouldDeleteUserAndAssignments_WhenUserExists()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = new AppUser { Id = userId };
        _userManagerMock.Setup(x => x.FindByIdAsync(userId.ToString()))
            .ReturnsAsync(user);
        _userManagerMock.Setup(x => x.DeleteAsync(user))
            .ReturnsAsync(IdentityResult.Success);

        var assignments = new List<MentorAssignment> { new MentorAssignment { MentorUserKey = userId } };
        _mentorAssignmentRepositoryMock.Setup(r => r.GetByMentorUserKeyAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(assignments);

        var command = new DeleteUserCommand(userId);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.Equal(ResultType.Success, result.Type);
        _mentorAssignmentRepositoryMock.Verify(r => r.Delete(It.Is<MentorAssignment>(a => a.MentorUserKey == userId), It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        _userManagerMock.Verify(x => x.DeleteAsync(user), Times.Once);
    }

    [Fact]
    public async Task Handle_ShouldReturnError_WhenDeleteFails()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = new AppUser { Id = userId };
        _userManagerMock.Setup(x => x.FindByIdAsync(userId.ToString()))
            .ReturnsAsync(user);
        _userManagerMock.Setup(x => x.DeleteAsync(user))
            .ReturnsAsync(IdentityResult.Failed(new IdentityError { Description = "Error deleting" }));

        _mentorAssignmentRepositoryMock.Setup(r => r.GetByMentorUserKeyAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<MentorAssignment>());

        var command = new DeleteUserCommand(userId);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.Equal(ResultType.InternalServerError, result.Type);
    }
}
