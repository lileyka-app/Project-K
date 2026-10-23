using FluentAssertions;
using Moq;
using ProjectK.BusinessLogic.Modules.KurinModule.Features.Agenda.Status;
using ProjectK.BusinessLogic.Modules.KurinModule.Services;
using ProjectK.BusinessLogic.Tests.TestHelpers;
using ProjectK.Common.Entities.KurinModule;
using ProjectK.Common.Entities.KurinModule.Agenda;
using ProjectK.Common.Models.Audit;
using ProjectK.Common.Interfaces;
using ProjectK.Common.Interfaces.Modules.InfrastructureModule;
using ProjectK.Common.Interfaces.Modules.KurinModule;
using ProjectK.Common.Interfaces.Modules.MemberModule;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Records;
using Xunit;

namespace ProjectK.BusinessLogic.Tests.KurinModule.HandlerTests.AgendaHandlers;

/// <summary>The dialog's move: one target, or one person's part of a target done «кожному окремо».</summary>
public class ChangeAgendaTargetStatusHandlerTests
{
    private readonly Mock<IUnitOfWork> _uow = new();
    private readonly Mock<IMemberDirectory> _members = new();
    private readonly Mock<IAgendaAccess> _access = new();
    private readonly Mock<ICurrentUserContext> _currentUser = new();
    private readonly Mock<IAgendaItemRepository> _agendaRepo = new();
    private readonly ChangeAgendaTargetStatusCommandHandler _handler;
    private readonly JournalRecorder _journal;

    private readonly Guid _kurinKey = Guid.NewGuid();
    private readonly Guid _groupKey = Guid.NewGuid();
    private readonly Guid _hurtkovyi = Guid.NewGuid();
    private readonly Guid _hurtkovyiUser = Guid.NewGuid();
    private readonly Guid _youth = Guid.NewGuid();
    private readonly AgendaAssignment _target;
    private readonly AgendaItem _item;

    public ChangeAgendaTargetStatusHandlerTests()
    {
        _target = new AgendaAssignment { TargetType = AgendaTargetType.Group, TargetKey = _groupKey, CompletionMode = AgendaCompletionMode.PerMember };
        _item = new AgendaItem
        {
            AgendaItemKey = Guid.NewGuid(),
            KurinKey = _kurinKey,
            Kind = AgendaItemKind.Task,
            Title = "Здати вкладку",
            CreatedByUserKey = _hurtkovyiUser,
            Assignments = [_target]
        };

        _uow.Setup(u => u.AgendaItems).Returns(_agendaRepo.Object);
        _agendaRepo.Setup(r => r.GetByKeyWithAssignmentsAsync(_item.AgendaItemKey, It.IsAny<CancellationToken>())).ReturnsAsync(_item);
        _currentUser.Setup(c => c.KurinKey).Returns(_kurinKey);
        _members.Setup(d => d.GetByKurinAsync(_kurinKey, It.IsAny<CancellationToken>())).ReturnsAsync(
        [
            new MemberSummary(_hurtkovyi, _hurtkovyiUser, _kurinKey, _groupKey, "Богдан", "Гончар", "b@x", null),
            new MemberSummary(_youth, Guid.NewGuid(), _kurinKey, _groupKey, "Марта", "Мельник", "m@x", null)
        ]);
        _members.Setup(d => d.GetByKurinAsync(_kurinKey, It.IsAny<MemberSelection>(), It.IsAny<CancellationToken>())).ReturnsAsync(
        [
            new MemberSummary(_hurtkovyi, _hurtkovyiUser, _kurinKey, _groupKey, "Богдан", "Гончар", "b@x", null),
            new MemberSummary(_youth, Guid.NewGuid(), _kurinKey, _groupKey, "Марта", "Мельник", "m@x", null)
        ]);
        _access.Setup(a => a.BuildViewerAsync(_kurinKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AgendaViewerContext(_kurinKey, _hurtkovyiUser, _hurtkovyi, _groupKey, [_groupKey], [], false, true, [_groupKey]));

        var groups = new Mock<IGroupRepository>();
        groups.Setup(g => g.GetAllAsync(_kurinKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync([new Group("Соколи", _kurinKey) { GroupKey = _groupKey }]);
        _uow.Setup(u => u.Groups).Returns(groups.Object);

        _journal = new JournalRecorder(_currentUser.Object).Knows(_youth, _kurinKey, _groupKey, "Марта", "Мельник");
        _handler = new ChangeAgendaTargetStatusCommandHandler(
            _uow.Object, _members.Object, _access.Object, _currentUser.Object, Mock.Of<IDomainEventPublisher>(), TimeProvider.System, _journal.Audit);
    }

    [Fact]
    public async Task APersonsPart_IsRecordedAboutThePerson()
    {
        await _handler.Handle(new ChangeAgendaTargetStatusCommand(_item.AgendaItemKey, _target.AgendaAssignmentKey, AgendaItemStatus.Done, _youth), default);

        var entry = _journal.Entries.Should().ContainSingle().Subject;
        entry.Action.Should().Be(AuditActions.Agenda.StatusChanged);
        entry.SubjectLabel.Should().Be("Здати вкладку");
        entry.SubjectMemberKey.Should().Be(_youth);
        entry.SubjectMemberName.Should().Be("Марта Мельник");
        entry.SubjectGroupKey.Should().Be(_groupKey);
        entry.Changes.Should().Equal(
            new AuditChange("status", "Todo", "Done"),
            new AuditChange("taskStatus", "Todo", "InProgress"));
    }

    [Fact]
    public async Task ASharedTarget_IsNamedInTheLabel()
    {
        _target.CompletionMode = AgendaCompletionMode.Shared;

        await _handler.Handle(new ChangeAgendaTargetStatusCommand(_item.AgendaItemKey, _target.AgendaAssignmentKey, AgendaItemStatus.Done, null), default);

        var entry = _journal.Entries.Should().ContainSingle().Subject;
        entry.SubjectLabel.Should().Be("Здати вкладку — Соколи");
        entry.SubjectMemberKey.Should().BeNull();
        entry.Changes.Should().Equal(
            new AuditChange("status", "Todo", "Done"),
            new AuditChange("taskStatus", "Todo", "Done"));
    }

    [Fact]
    public async Task TheHurtkovyi_MarksAYouthsPart_AndTheTargetFollows()
    {
        var result = await _handler.Handle(new ChangeAgendaTargetStatusCommand(_item.AgendaItemKey, _target.AgendaAssignmentKey, AgendaItemStatus.Done, _youth), default);

        result.Type.Should().Be(ResultType.Success);
        _target.Progress.Should().ContainSingle(p => p.MemberKey == _youth && p.Status == AgendaItemStatus.Done && p.ChangedByUserKey == _hurtkovyiUser);
        _target.Status.Should().Be(AgendaItemStatus.InProgress, "the гуртковий's own part is still to do");
        _item.Status.Should().Be(AgendaItemStatus.InProgress);
        _agendaRepo.Verify(r => r.AddProgress(It.IsAny<AgendaAssignmentProgress>()), Times.Once);
    }

    [Fact]
    public async Task APartOfSomeoneOutsideTheTarget_IsRefused()
    {
        var result = await _handler.Handle(new ChangeAgendaTargetStatusCommand(_item.AgendaItemKey, _target.AgendaAssignmentKey, AgendaItemStatus.Done, Guid.NewGuid()), default);

        result.Type.Should().Be(ResultType.BadRequest);
        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ATargetDoneEachOnTheirOwn_HasNoSingleStateToSet()
    {
        var result = await _handler.Handle(new ChangeAgendaTargetStatusCommand(_item.AgendaItemKey, _target.AgendaAssignmentKey, AgendaItemStatus.Done, null), default);

        result.Type.Should().Be(ResultType.BadRequest);
    }
}
