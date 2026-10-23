using FluentAssertions;
using Moq;
using ProjectK.BusinessLogic.Modules.KurinModule.Features.Member.Delete;
using ProjectK.BusinessLogic.Tests.TestHelpers;
using ProjectK.Common.Interfaces;
using ProjectK.Common.Interfaces.Modules.InfrastructureModule;
using ProjectK.Common.Interfaces.Modules.KurinModule;
using ProjectK.Common.Interfaces.Modules.MemberModule;
using ProjectK.Common.Models.Audit;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Records;
using Xunit;

namespace ProjectK.BusinessLogic.Tests.KurinModule.HandlerTests.MemberHandlers;

/// <summary>
/// Deleting a member is now two statements: clear what the kurin keeps pointed at them, and ask
/// the member module to remove them. What removal drags along — progress, history — is the
/// module's business and is covered where that lives.
/// </summary>
public class DeleteMemberHandlerTests
{
    private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();
    private readonly Mock<IMemberDirectory> _memberDirectory = new();
    private readonly Mock<IAgendaItemRepository> _agendaItemRepositoryMock = new();
    private readonly DeleteMemberCommandHandler _handler;
    private readonly Guid _kurinKey = Guid.NewGuid();
    private readonly JournalRecorder _journal;

    public DeleteMemberHandlerTests()
    {
        _unitOfWorkMock.Setup(u => u.AgendaItems).Returns(_agendaItemRepositoryMock.Object);
        _unitOfWorkMock
            .Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        _memberDirectory
            .Setup(d => d.ExistsAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _memberDirectory
            .Setup(d => d.RemoveAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var user = new Mock<ICurrentUserContext>();
        user.SetupGet(u => u.KurinKey).Returns(_kurinKey);
        _journal = new JournalRecorder(user.Object);
        _handler = new DeleteMemberCommandHandler(_unitOfWorkMock.Object, _memberDirectory.Object, _journal.Audit);
    }

    [Fact]
    public async Task Handle_WithoutAKey_ShouldReturnBadRequest()
    {
        var result = await _handler.Handle(new DeleteMemberCommand(Guid.Empty), CancellationToken.None);

        result.Type.Should().Be(ResultType.BadRequest);
        result.ErrorCode.Should().Be("MemberKeyRequired");
    }

    [Fact]
    public async Task Handle_WhenMemberDoesNotExist_ShouldReturnNotFound()
    {
        _memberDirectory
            .Setup(d => d.ExistsAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var result = await _handler.Handle(new DeleteMemberCommand(Guid.NewGuid()), CancellationToken.None);

        result.Type.Should().Be(ResultType.NotFound);
        _memberDirectory.Verify(
            d => d.RemoveAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_ShouldClearAgendaAssignmentsBeforeRemovingThePerson()
    {
        var memberKey = Guid.NewGuid();

        var result = await _handler.Handle(new DeleteMemberCommand(memberKey), CancellationToken.None);

        result.Type.Should().Be(ResultType.Success);
        _agendaItemRepositoryMock.Verify(
            r => r.RemoveAssignmentsForTargetsAsync(
                It.Is<IReadOnlyCollection<Guid>>(keys => keys.Single() == memberKey),
                It.IsAny<CancellationToken>()),
            Times.Once);
        _memberDirectory.Verify(
            d => d.RemoveAsync(memberKey, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_WhenNothingWasWritten_ShouldReturnInternalServerError()
    {
        _unitOfWorkMock
            .Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        var result = await _handler.Handle(new DeleteMemberCommand(Guid.NewGuid()), CancellationToken.None);

        result.Type.Should().Be(ResultType.InternalServerError);
        result.ErrorCode.Should().Be("MemberDeleteFailed");
    }

    [Fact]
    public async Task Handle_WhenRemovalThrows_ShouldPropagateException()
    {
        _memberDirectory
            .Setup(d => d.RemoveAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("boom"));

        var act = async () => await _handler.Handle(new DeleteMemberCommand(Guid.NewGuid()), CancellationToken.None);

        await act.Should().ThrowAsync<Exception>().WithMessage("boom");
    }

    [Fact]
    public async Task Handle_ShouldRecordWhoWasDeleted_WhileTheyCanStillBeNamed()
    {
        var memberKey = Guid.NewGuid();
        var groupKey = Guid.NewGuid();
        _journal.Knows(memberKey, _kurinKey, groupKey, "Олесь", "Юнак");
        _memberDirectory
            .Setup(d => d.FindAsync(memberKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MemberSummary(memberKey, null, _kurinKey, groupKey, "Олесь", "Юнак", "o@x", null));

        await _handler.Handle(new DeleteMemberCommand(memberKey), CancellationToken.None);

        var entry = _journal.Entries.Should().ContainSingle().Subject;
        entry.Area.Should().Be(AuditArea.Member);
        entry.Action.Should().Be(AuditActions.Member.Deleted);
        entry.KurinKey.Should().Be(_kurinKey);
        entry.SubjectKey.Should().Be(memberKey);
        entry.SubjectLabel.Should().Be("Олесь Юнак");
        entry.SubjectMemberName.Should().Be("Олесь Юнак");
        entry.SubjectGroupKey.Should().Be(groupKey);
        entry.Changes.Should().Equal(
            new AuditChange("firstName", "Олесь", null),
            new AuditChange("lastName", "Юнак", null));
    }
}
