using Moq;
using ProjectK.BusinessLogic.Modules.ProbesAndBadgesModule.Services;
using ProjectK.BusinessLogic.Services.Events;
using ProjectK.Common.Interfaces;
using ProjectK.Common.Interfaces.Modules.ProbesAndBadgesModule;
using ProjectK.Common.Models.Events;
using Xunit;

namespace ProjectK.BusinessLogic.Tests.ProbesAndBadgesModule.HandlerTests;

/// <summary>
/// No foreign key ties progress to a member any more, so nothing cascades when one is removed. The
/// module that owns the rows clears them itself, on hearing that the people are gone.
/// </summary>
public class ProgressCleanupHandlerTests
{
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly Mock<IProbeProgressRepository> _probes = new();
    private readonly Mock<IProbePointProgressRepository> _points = new();
    private readonly Mock<IBadgeProgressRepository> _badges = new();
    private readonly Mock<IProbeProjectRepository> _projects = new();
    private readonly ProgressCleanupEventHandler _handler;

    public ProgressCleanupHandlerTests()
    {
        _unitOfWork.Setup(u => u.ProbeProgresses).Returns(_probes.Object);
        _unitOfWork.Setup(u => u.ProbePointProgresses).Returns(_points.Object);
        _unitOfWork.Setup(u => u.BadgeProgresses).Returns(_badges.Object);
        _unitOfWork.Setup(u => u.ProbeProjects).Returns(_projects.Object);
        _handler = new ProgressCleanupEventHandler(_unitOfWork.Object);
    }

    [Fact]
    public async Task Handle_ShouldClearProbesPointsAndBadges_ForEveryoneRemoved()
    {
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();

        await _handler.Handle(
            new DomainEventNotification<MembersRemoved>(new MembersRemoved([first, second])),
            CancellationToken.None);

        _probes.Verify(r => r.DeleteForMembersAsync(
            It.Is<IReadOnlyCollection<Guid>>(keys => keys.Count == 2 && keys.Contains(first) && keys.Contains(second)),
            It.IsAny<CancellationToken>()),
            Times.Once);
        _points.Verify(r => r.DeleteForMembersAsync(
            It.Is<IReadOnlyCollection<Guid>>(keys => keys.Count == 2),
            It.IsAny<CancellationToken>()),
            Times.Once);
        _badges.Verify(r => r.DeleteForMembersAsync(
            It.Is<IReadOnlyCollection<Guid>>(keys => keys.Count == 2),
            It.IsAny<CancellationToken>()),
            Times.Once);
        _projects.Verify(r => r.DeleteForMembersAsync(
            It.Is<IReadOnlyCollection<Guid>>(keys => keys.Count == 2),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_WithNobodyRemoved_ShouldTouchNothing()
    {
        await _handler.Handle(
            new DomainEventNotification<MembersRemoved>(new MembersRemoved([])),
            CancellationToken.None);

        _probes.Verify(r => r.DeleteForMembersAsync(
            It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
