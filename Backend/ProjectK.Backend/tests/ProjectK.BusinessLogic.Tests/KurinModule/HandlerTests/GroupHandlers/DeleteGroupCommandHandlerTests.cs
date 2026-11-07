using System;
using System.Threading.Tasks;
using FluentAssertions;
using ProjectK.BusinessLogic.Tests.TestHelpers;
using Moq;
using ProjectK.BusinessLogic.Modules.KurinModule.Features.Group.Delete;
using ProjectK.BusinessLogic.Services.Caching;
using ProjectK.Common.Entities.KurinModule;
using ProjectK.Common.Interfaces;
using ProjectK.Common.Interfaces.Modules.DuesModule;
using ProjectK.Common.Interfaces.Modules.KurinModule;
using ProjectK.Common.Models.Audit;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Records;
using Xunit;

namespace ProjectK.BusinessLogic.Tests.KurinModule.HandlerTests.GroupHandlers;

public class DeleteGroupHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 1, 12, 0, 0, TimeSpan.Zero);

    private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();
    private readonly Mock<IGroupRepository> _groupRepositoryMock = new();
    private readonly Mock<ILeadershipRepository> _leadershipRepositoryMock = new();
    private readonly Mock<IMembershipRepository> _membershipRepositoryMock = new();
    private readonly Mock<IMentorAssignmentRepository> _mentorRepositoryMock = new();
    private readonly Mock<IAgendaItemRepository> _agendaItemRepositoryMock = new();
    private readonly Mock<IKurinAudit> _auditMock = new();
    private readonly Mock<IDuesDirectory> _dues = new();
    private readonly DeleteGroupCommandHandler _handler;

    public DeleteGroupHandlerTests()
    {
        _unitOfWorkMock.Setup(u => u.Groups).Returns(_groupRepositoryMock.Object);
        _unitOfWorkMock.Setup(u => u.Leaderships).Returns(_leadershipRepositoryMock.Object);
        _unitOfWorkMock.Setup(u => u.AgendaItems).Returns(_agendaItemRepositoryMock.Object);
        _unitOfWorkMock.Setup(u => u.Memberships).Returns(_membershipRepositoryMock.Object);
        _unitOfWorkMock.Setup(u => u.MentorAssignments).Returns(_mentorRepositoryMock.Object);
        _leadershipRepositoryMock
            .Setup(r => r.CloseForGroupAsync(It.IsAny<Guid>(), It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        _dues.Setup(d => d.GroupBoxSettledAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _mentorRepositoryMock
            .Setup(r => r.GetByGroupKeyAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        _handler = new DeleteGroupCommandHandler(_unitOfWorkMock.Object, Mock.Of<IBackendCache>(), _auditMock.Object, new FixedTimeProvider(Now), _dues.Object);
    }

    [Fact]
    public async Task Handle_DissolvesTheGroupAndKeepsItsProvid()
    {
        // The archive keeps a dissolved гурток with its провід: deleting them erased who led whom.
        var groupKey = Guid.NewGuid();
        var group = new Group("Alpha", Guid.NewGuid()) { GroupKey = groupKey };
        _groupRepositoryMock.Setup(r => r.GetByKeyAsync(groupKey, default)).ReturnsAsync(group);
        _unitOfWorkMock.Setup(u => u.SaveChangesAsync(default)).ReturnsAsync(1);

        var result = await _handler.Handle(new DeleteGroupCommand(groupKey), default);

        result.Type.Should().Be(ResultType.Success);
        group.DissolvedAtUtc.Should().Be(Now.UtcDateTime);
        _leadershipRepositoryMock.Verify(r => r.CloseForGroupAsync(groupKey, new DateOnly(2026, 11, 1), default), Times.Once);
        _leadershipRepositoryMock.Verify(r => r.DeleteForGroupAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
        _groupRepositoryMock.Verify(r => r.Delete(It.IsAny<Group>(), It.IsAny<CancellationToken>()), Times.Never);
        _membershipRepositoryMock.Verify(r => r.DetachFromGroupAsync(groupKey, default), Times.Once);
        _auditMock.Verify(a => a.RecordAsync(
            It.Is<AuditRecord>(record => record.Action == AuditActions.Archive.GroupDissolved && record.SubjectKey == groupKey),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_RefusesWhileTheGroupsBoxIsNotSettled()
    {
        // A dissolved гурток's box is read nowhere: money left in it would be stuck for good.
        var groupKey = Guid.NewGuid();
        var group = new Group("Alpha", Guid.NewGuid()) { GroupKey = groupKey };
        _groupRepositoryMock.Setup(r => r.GetByKeyAsync(groupKey, default)).ReturnsAsync(group);
        _dues.Setup(d => d.GroupBoxSettledAsync(group.KurinKey, groupKey, It.IsAny<CancellationToken>())).ReturnsAsync(false);

        var result = await _handler.Handle(new DeleteGroupCommand(groupKey), default);

        result.Type.Should().Be(ResultType.Conflict);
        result.ErrorCode.Should().Be("GroupBoxNotSettled");
        group.DissolvedAtUtc.Should().BeNull();
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(default), Times.Never);
    }

    [Fact]
    public async Task Handle_RevokesTheGroupsMentors()
    {
        var groupKey = Guid.NewGuid();
        var assignment = new Common.Entities.KurinModule.MentorAssignment { GroupKey = groupKey, MentorUserKey = Guid.NewGuid() };
        _groupRepositoryMock.Setup(r => r.GetByKeyAsync(groupKey, default)).ReturnsAsync(new Group("Alpha", Guid.NewGuid()) { GroupKey = groupKey });
        _mentorRepositoryMock.Setup(r => r.GetByGroupKeyAsync(groupKey, default)).ReturnsAsync([assignment]);
        _unitOfWorkMock.Setup(u => u.SaveChangesAsync(default)).ReturnsAsync(1);

        await _handler.Handle(new DeleteGroupCommand(groupKey), default);

        assignment.RevokedAtUtc.Should().Be(Now.UtcDateTime);
    }

    [Fact]
    public async Task Handle_WhenGroupDoesNotExist_ShouldReturnNotFound()
    {
        var groupKey = Guid.NewGuid();
        _groupRepositoryMock.Setup(r => r.GetByKeyAsync(groupKey, default)).ReturnsAsync((Group)null!);

        var result = await _handler.Handle(new DeleteGroupCommand(groupKey), default);

        result.Type.Should().Be(ResultType.NotFound);
        result.ErrorCode.Should().Be("GROUP_NOT_FOUND");
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(default), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenGroupKeyIsEmpty_ShouldReturnBadRequest()
    {
        var result = await _handler.Handle(new DeleteGroupCommand(Guid.Empty), default);

        result.Type.Should().Be(ResultType.BadRequest);
        result.ErrorCode.Should().Be("GROUP_KEY_EMPTY");
        _groupRepositoryMock.Verify(r => r.GetByKeyAsync(It.IsAny<Guid>(), default), Times.Never);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(default), Times.Never);
    }
}
