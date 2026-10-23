using FluentAssertions;
using Moq;
using ProjectK.BusinessLogic.Modules.KurinModule.Features.Agenda.Status;
using ProjectK.BusinessLogic.Modules.KurinModule.Services;
using ProjectK.BusinessLogic.Tests.TestHelpers;
using ProjectK.Common.Entities.KurinModule.Agenda;
using ProjectK.Common.Models.Audit;
using ProjectK.Common.Interfaces;
using ProjectK.Common.Interfaces.Modules.InfrastructureModule;
using ProjectK.Common.Interfaces.Modules.KurinModule;
using ProjectK.Common.Interfaces.Modules.MemberModule;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Events;
using ProjectK.Common.Models.Records;
using Xunit;

namespace ProjectK.BusinessLogic.Tests.KurinModule.HandlerTests.AgendaHandlers;

public class ChangeAgendaItemStatusHandlerTests
{
    private readonly Mock<IUnitOfWork> _uow = new();
    private readonly Mock<IMemberDirectory> _memberDirectory = new();
    private readonly Mock<IAgendaAccess> _access = new();
    private readonly Mock<ICurrentUserContext> _currentUser = new();
    private readonly Mock<IDomainEventPublisher> _events = new();
    private readonly Mock<IAgendaItemRepository> _agendaRepo = new();
    private readonly ChangeAgendaItemStatusCommandHandler _handler;
    private readonly JournalRecorder _journal;

    private readonly Guid _kurinKey = Guid.NewGuid();
    private readonly Guid _memberKey = Guid.NewGuid();
    private readonly Guid _actorUserKey = Guid.NewGuid();

    public ChangeAgendaItemStatusHandlerTests()
    {
        _uow.Setup(u => u.AgendaItems).Returns(_agendaRepo.Object);
        _currentUser.Setup(c => c.KurinKey).Returns(_kurinKey);
        _memberDirectory.Setup(d => d.GetByKurinAsync(_kurinKey, It.IsAny<CancellationToken>())).ReturnsAsync([]);
        _memberDirectory.Setup(d => d.GetByKurinAsync(_kurinKey, It.IsAny<MemberSelection>(), It.IsAny<CancellationToken>())).ReturnsAsync([]);
        _journal = new JournalRecorder(_currentUser.Object);
        _handler = new ChangeAgendaItemStatusCommandHandler(_uow.Object, _memberDirectory.Object, _access.Object, _currentUser.Object, _events.Object, TimeProvider.System, _journal.Audit);
    }

    private AgendaItem TaskAssignedToMember(Guid? createdBy = null)
    {
        return new AgendaItem
        {
            AgendaItemKey = Guid.NewGuid(),
            KurinKey = _kurinKey,
            Kind = AgendaItemKind.Task,
            Title = "Прибирання",
            Status = AgendaItemStatus.Todo,
            // Creator == actor keeps the notification path quiet on the success cases.
            CreatedByUserKey = createdBy ?? _actorUserKey,
            Assignments = new List<AgendaAssignment>
            {
                new() { TargetType = AgendaTargetType.Member, TargetKey = _memberKey }
            }
        };
    }

    private void SetupViewer(bool canSeeWholeKurin, bool isLeadership, Guid? viewerMemberKey)
    {
        _access.Setup(a => a.BuildViewerAsync(_kurinKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AgendaViewerContext(
                KurinKey: _kurinKey,
                ViewerUserKey: _actorUserKey,
                ViewerMemberKey: viewerMemberKey,
                ViewerOwnGroupKey: null,
                VisibilityGroupKeys: Array.Empty<Guid>(),
                ViewerLeadershipKeys: Array.Empty<Guid>(),
                CanSeeWholeKurin: canSeeWholeKurin,
                IsLeadership: isLeadership));
    }

    [Fact]
    public async Task Handle_WhenViewerIsAssignee_ChangesStatus()
    {
        var item = TaskAssignedToMember();
        _agendaRepo.Setup(r => r.GetByKeyWithAssignmentsAsync(item.AgendaItemKey, It.IsAny<CancellationToken>())).ReturnsAsync(item);
        SetupViewer(canSeeWholeKurin: false, isLeadership: false, viewerMemberKey: _memberKey);

        var result = await _handler.Handle(new ChangeAgendaItemStatusCommand(item.AgendaItemKey, AgendaItemStatus.Done), default);

        result.Type.Should().Be(ResultType.Success);
        item.Status.Should().Be(AgendaItemStatus.Done);
        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);

        var entry = _journal.Entries.Should().ContainSingle().Subject;
        entry.Area.Should().Be(AuditArea.Agenda);
        entry.Action.Should().Be(AuditActions.Agenda.StatusChanged);
        entry.KurinKey.Should().Be(_kurinKey);
        entry.SubjectKey.Should().Be(item.AgendaItemKey);
        entry.SubjectLabel.Should().Be("Прибирання");
        entry.Changes.Should().Equal(
            new AuditChange("status", "Todo", "Done"),
            new AuditChange("taskStatus", "Todo", "Done"));
    }

    [Fact]
    public async Task Handle_WhenNothingMoves_RecordsNothing()
    {
        var item = TaskAssignedToMember();
        _agendaRepo.Setup(r => r.GetByKeyWithAssignmentsAsync(item.AgendaItemKey, It.IsAny<CancellationToken>())).ReturnsAsync(item);
        SetupViewer(canSeeWholeKurin: false, isLeadership: false, viewerMemberKey: _memberKey);

        var result = await _handler.Handle(new ChangeAgendaItemStatusCommand(item.AgendaItemKey, AgendaItemStatus.Todo), default);

        result.Type.Should().Be(ResultType.Success);
        _journal.Entries.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_ATaskWithoutTargets_IsRecordedAsTheTaskMoving()
    {
        var item = TaskAssignedToMember();
        item.Assignments.Clear();
        _agendaRepo.Setup(r => r.GetByKeyWithAssignmentsAsync(item.AgendaItemKey, It.IsAny<CancellationToken>())).ReturnsAsync(item);
        SetupViewer(canSeeWholeKurin: true, isLeadership: true, viewerMemberKey: null);

        await _handler.Handle(new ChangeAgendaItemStatusCommand(item.AgendaItemKey, AgendaItemStatus.InProgress), default);

        _journal.Entries.Single().Changes.Should().Equal(new AuditChange("status", "Todo", "InProgress"));
    }

    [Fact]
    public async Task Handle_WhenViewerIsUnrelatedMember_ReturnsForbidden()
    {
        // A different creator so the "creator may manage" branch does not apply.
        var item = TaskAssignedToMember(createdBy: Guid.NewGuid());
        _agendaRepo.Setup(r => r.GetByKeyWithAssignmentsAsync(item.AgendaItemKey, It.IsAny<CancellationToken>())).ReturnsAsync(item);
        SetupViewer(canSeeWholeKurin: false, isLeadership: false, viewerMemberKey: Guid.NewGuid());

        var result = await _handler.Handle(new ChangeAgendaItemStatusCommand(item.AgendaItemKey, AgendaItemStatus.Done), default);

        result.Type.Should().Be(ResultType.Forbidden);
        item.Status.Should().Be(AgendaItemStatus.Todo);
        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenViewerLeadsKurin_ChangesStatus()
    {
        var item = TaskAssignedToMember();
        _agendaRepo.Setup(r => r.GetByKeyWithAssignmentsAsync(item.AgendaItemKey, It.IsAny<CancellationToken>())).ReturnsAsync(item);
        SetupViewer(canSeeWholeKurin: true, isLeadership: true, viewerMemberKey: null);

        var result = await _handler.Handle(new ChangeAgendaItemStatusCommand(item.AgendaItemKey, AgendaItemStatus.InProgress), default);

        result.Type.Should().Be(ResultType.Success);
        item.Status.Should().Be(AgendaItemStatus.InProgress);
    }
}
