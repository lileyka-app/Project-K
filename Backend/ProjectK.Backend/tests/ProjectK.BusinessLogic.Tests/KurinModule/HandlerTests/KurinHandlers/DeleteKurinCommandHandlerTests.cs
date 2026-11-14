using FluentAssertions;
using MediatR;
using Moq;
using ProjectK.BusinessLogic.Modules.KurinModule.Features.Kurin;
using ProjectK.BusinessLogic.Services.Caching;
using ProjectK.Common.Contracts.AuthModule;
using ProjectK.Common.Contracts.KurinModule;
using ProjectK.Common.Contracts.ProbesAndBadgesModule;
using ProjectK.Common.Contracts.Shared;
using ProjectK.Common.Entities.KurinModule;
using ProjectK.Common.Models.Enums;

namespace ProjectK.BusinessLogic.Tests.KurinModule.HandlerTests.KurinHandlers;

public class DeleteKurinHandlerTests
{
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly Mock<IProbeProgressRepository> _probeProgressRepositoryMock;
    private readonly Mock<IProbePointProgressRepository> _probePointProgressRepositoryMock;
    private readonly Mock<IBadgeProgressRepository> _badgeProgressRepositoryMock;
    private readonly Mock<IKurinRepository> _kurinRepositoryMock;
    private readonly DeleteKurinCommandHandler _handler;
    private readonly Mock<IMemberRepository> _memberRepositoryMock;
    private readonly Mock<ILeadershipRepository> _leadershipRepositoryMock;
    private readonly Mock<IMembershipRepository> _membershipRepositoryMock = new();
    private readonly Mock<IAppUserRepository> _usersMock = new();
    private readonly Mock<IBackendCache> _cacheMock;

    public DeleteKurinHandlerTests()
    {
        _kurinRepositoryMock = new Mock<IKurinRepository>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _memberRepositoryMock = new Mock<IMemberRepository>();
        _leadershipRepositoryMock = new Mock<ILeadershipRepository>();
        _cacheMock = new Mock<IBackendCache>();

        _unitOfWorkMock.Setup(uow => uow.Kurins).Returns(_kurinRepositoryMock.Object);
        _unitOfWorkMock.Setup(uow => uow.Memberships).Returns(_membershipRepositoryMock.Object);
        _unitOfWorkMock.Setup(uow => uow.Leaderships).Returns(_leadershipRepositoryMock.Object);
        _unitOfWorkMock.Setup(uow => uow.Users).Returns(_usersMock.Object);
        _leadershipRepositoryMock
            .Setup(r => r.DeleteForKurinAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        _probeProgressRepositoryMock = new Mock<IProbeProgressRepository>();
        _probePointProgressRepositoryMock = new Mock<IProbePointProgressRepository>();
        _badgeProgressRepositoryMock = new Mock<IBadgeProgressRepository>();
        _unitOfWorkMock.Setup(uow => uow.ProbeProgresses).Returns(_probeProgressRepositoryMock.Object);
        _unitOfWorkMock.Setup(uow => uow.ProbePointProgresses).Returns(_probePointProgressRepositoryMock.Object);
        _unitOfWorkMock.Setup(uow => uow.BadgeProgresses).Returns(_badgeProgressRepositoryMock.Object);

        _handler = new DeleteKurinCommandHandler(_unitOfWorkMock.Object, _cacheMock.Object);
    }

    [Fact]
    public async Task Handle_WhenKurinExists_ShouldDeleteKurinAndReturnSuccess()
    {
        // Arrange
        var kurinKey = Guid.NewGuid();
        var kurin = new Kurin(1) { KurinKey = kurinKey };
        var command = new DeleteKurinCommand(kurinKey);

        _kurinRepositoryMock.Setup(r => r.GetByKeyAsync(kurinKey, default))
            .ReturnsAsync(kurin);
        _unitOfWorkMock.Setup(u => u.SaveChangesAsync(default))
            .ReturnsAsync(1);

        // Act
        var result = await _handler.Handle(command, default);

        // Assert
        result.Type.Should().Be(ResultType.Success);
        result.Data.Should().Be(Unit.Value);
        _kurinRepositoryMock.Verify(r => r.Delete(kurin, default), Times.Once);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(default), Times.Once);
    }

    /// <summary>
    /// STAB-05: an account's chosen kurin is a bare key, so the handler has to forget it on every
    /// account that names this kurin — otherwise they sign in to a kurin that no longer exists.
    /// </summary>
    [Fact]
    public async Task Handle_WhenKurinExists_ShouldDetachEveryAccountThatStoodInIt_BeforeSaving()
    {
        var kurinKey = Guid.NewGuid();
        var kurin = new Kurin(1) { KurinKey = kurinKey };
        _kurinRepositoryMock.Setup(r => r.GetByKeyAsync(kurinKey, default)).ReturnsAsync(kurin);
        _unitOfWorkMock.Setup(u => u.SaveChangesAsync(default)).ReturnsAsync(1);

        var order = new List<string>();
        _usersMock
            .Setup(u => u.DetachFromKurinAsync(kurinKey, It.IsAny<CancellationToken>()))
            .Callback(() => order.Add("detach"))
            .Returns(Task.CompletedTask);
        _unitOfWorkMock
            .Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Callback(() => order.Add("save"))
            .ReturnsAsync(1);

        await _handler.Handle(new DeleteKurinCommand(kurinKey), default);

        _usersMock.Verify(u => u.DetachFromKurinAsync(kurinKey, It.IsAny<CancellationToken>()), Times.Once);
        order.Should().Equal("detach", "save");
    }

    [Fact]
    public async Task Handle_WhenKurinDoesNotExist_ShouldReturnNotFound()
    {
        // Arrange
        var kurinKey = Guid.NewGuid();
        var command = new DeleteKurinCommand(kurinKey);

        _kurinRepositoryMock.Setup(r => r.GetByKeyAsync(kurinKey, default))
            .ReturnsAsync((Kurin)null!);

        // Act
        var result = await _handler.Handle(command, default);

        // Assert
        result.Type.Should().Be(ResultType.NotFound);
        result.ErrorMessage.Should().Be($"Kurin with key {kurinKey} not found.");
        _kurinRepositoryMock.Verify(r => r.Delete(It.IsAny<Kurin>(), default), Times.Never);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(default), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenKurinKeyIsEmpty_ShouldReturnInvalidData()
    {
        // Arrange
        var command = new DeleteKurinCommand(Guid.Empty);

        // Act
        var result = await _handler.Handle(command, default);

        // Assert
        result.Type.Should().Be(ResultType.BadRequest);
        result.ErrorMessage.Should().Be("KurinKey cannot be empty.");
        _kurinRepositoryMock.Verify(r => r.GetByKeyAsync(It.IsAny<Guid>(), default), Times.Never);
        _kurinRepositoryMock.Verify(r => r.Delete(It.IsAny<Kurin>(), default), Times.Never);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(default), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenSaveChangesFails_ShouldReturnServerError()
    {
        // Arrange
        var kurinKey = Guid.NewGuid();
        var kurin = new Kurin(1) { KurinKey = kurinKey };
        var command = new DeleteKurinCommand(kurinKey);

        _kurinRepositoryMock.Setup(r => r.GetByKeyAsync(kurinKey, default))
            .ReturnsAsync(kurin);
        _unitOfWorkMock.Setup(u => u.SaveChangesAsync(default))
            .ReturnsAsync(0);

        // Act
        var result = await _handler.Handle(command, default);

        // Assert
        result.Type.Should().Be(ResultType.InternalServerError);
        result.ErrorMessage.Should().Be("Failed to delete Kurin due to internal error.");
        _kurinRepositoryMock.Verify(r => r.Delete(kurin, default), Times.Once);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(default), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenDeleteThrowsException_ShouldPropagateException()
    {
        // Arrange
        var kurinKey = Guid.NewGuid();
        var kurin = new Kurin(1) { KurinKey = kurinKey };
        var command = new DeleteKurinCommand(kurinKey);
        var expectedException = new Exception("Test exception");

        _kurinRepositoryMock.Setup(r => r.GetByKeyAsync(kurinKey, default))
            .ReturnsAsync(kurin);
        _kurinRepositoryMock.Setup(r => r.Delete(kurin, default))
            .Throws(expectedException);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<Exception>(() => _handler.Handle(command, default));
        exception.Should().BeSameAs(expectedException);
    }
}
