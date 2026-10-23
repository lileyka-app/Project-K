using FluentAssertions;
using Moq;
using ProjectK.BusinessLogic.Modules.KurinModule.Features.Agenda.Update;
using ProjectK.BusinessLogic.Modules.KurinModule.Services;
using ProjectK.BusinessLogic.Tests.TestHelpers;
using ProjectK.Common.Entities.KurinModule;
using ProjectK.Common.Models.Audit;
using ProjectK.Common.Entities.KurinModule.Agenda;
using ProjectK.Common.Interfaces;
using ProjectK.Common.Interfaces.Modules.InfrastructureModule;
using ProjectK.Common.Interfaces.Modules.KurinModule;
using ProjectK.Common.Interfaces.Modules.MemberModule;
using ProjectK.Common.Models.Dtos;
using ProjectK.Common.Models.Dtos.KurinModule;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Events;
using ProjectK.Common.Models.Records;
using Xunit;

namespace ProjectK.BusinessLogic.Tests.KurinModule.HandlerTests.AgendaHandlers;

public class UpdateAgendaItemHandlerTests
{
    private readonly Mock<IUnitOfWork> _uow = new();
    private readonly Mock<IMemberDirectory> _memberDirectory = new();
    private readonly Mock<IAgendaAccess> _access = new();
    private readonly Mock<ICurrentUserContext> _currentUser = new();
    private readonly Mock<IDomainEventPublisher> _events = new();
    private readonly Mock<IAgendaItemRepository> _agendaRepo = new();
    private readonly Mock<IMemberRepository> _memberRepo = new();
    private readonly UpdateAgendaItemCommandHandler _handler;
    private readonly JournalRecorder _journal;
    private readonly List<Group> _groups = [];

    private readonly Guid _kurinKey = Guid.NewGuid();

    public UpdateAgendaItemHandlerTests()
    {
        _uow.Setup(u => u.AgendaItems).Returns(_agendaRepo.Object);
        _memberDirectory.Setup(r => r.GetByKurinAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<MemberSummary>());
        _memberDirectory.Setup(r => r.GetByKurinAsync(It.IsAny<Guid>(), It.IsAny<MemberSelection>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<MemberSummary>());
        _currentUser.Setup(c => c.KurinKey).Returns(_kurinKey);
        _access.Setup(a => a.BuildViewerAsync(_kurinKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AgendaViewerContext(_kurinKey, Guid.NewGuid(), null, null, Array.Empty<Guid>(), Array.Empty<Guid>(), true, true));
        _access.Setup(a => a.AuthorizeTargetAsync(It.IsAny<AgendaTargetInput>(), ResourceAction.Create, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ResourceAccessDecision.Allow());
        var groups = new Mock<IGroupRepository>();
        groups.Setup(g => g.GetAllAsync(_kurinKey, It.IsAny<CancellationToken>())).ReturnsAsync(_groups);
        _uow.Setup(u => u.Groups).Returns(groups.Object);
        _journal = new JournalRecorder(_currentUser.Object);
        _handler = new UpdateAgendaItemCommandHandler(_uow.Object, _memberDirectory.Object, _access.Object, _currentUser.Object, _events.Object, _journal.Audit);
    }

    [Fact]
    public async Task Handle_WhenNothingChanges_RecordsNothing()
    {
        var item = new AgendaItem
        {
            AgendaItemKey = Guid.NewGuid(),
            KurinKey = _kurinKey,
            Kind = AgendaItemKind.Event,
            Title = "Табір",
            Location = "Карпати",
            Assignments = new List<AgendaAssignment> { new() { TargetType = AgendaTargetType.Kurin, TargetKey = _kurinKey } }
        };
        _agendaRepo.Setup(r => r.GetByKeyWithAssignmentsAsync(item.AgendaItemKey, It.IsAny<CancellationToken>())).ReturnsAsync(item);

        var result = await _handler.Handle(new UpdateAgendaItemCommand
        {
            AgendaItemKey = item.AgendaItemKey,
            Kind = AgendaItemKind.Event,
            Title = " Табір ",
            Location = "Карпати",
            Targets = [new() { TargetType = AgendaTargetType.Kurin, TargetKey = _kurinKey }]
        }, default);

        result.Type.Should().Be(ResultType.Success);
        _journal.Entries.Should().BeEmpty();
    }

    // Regression: stretching an event by adding an end date while keeping the same target must not
    // churn the assignment rows (delete + re-insert would trip the unique index / concurrency check).
    [Fact]
    public async Task Handle_WhenAddingEndDateWithSameTarget_UpdatesInPlaceWithoutChurn()
    {
        var kurinTarget = new AgendaAssignment { TargetType = AgendaTargetType.Kurin, TargetKey = _kurinKey };
        var item = new AgendaItem
        {
            AgendaItemKey = Guid.NewGuid(),
            KurinKey = _kurinKey,
            Kind = AgendaItemKind.Event,
            Title = "Табір",
            StartUtc = new DateTime(2026, 8, 10, 0, 0, 0, DateTimeKind.Utc),
            EndUtc = null,
            Assignments = new List<AgendaAssignment> { kurinTarget }
        };
        _agendaRepo.Setup(r => r.GetByKeyWithAssignmentsAsync(item.AgendaItemKey, It.IsAny<CancellationToken>())).ReturnsAsync(item);

        var request = new UpdateAgendaItemCommand
        {
            AgendaItemKey = item.AgendaItemKey,
            Kind = AgendaItemKind.Event,
            Title = "Табір",
            StartUtc = item.StartUtc,
            EndUtc = new DateTime(2026, 8, 14, 0, 0, 0, DateTimeKind.Utc),
            Targets = new List<AgendaTargetInput> { new() { TargetType = AgendaTargetType.Kurin, TargetKey = _kurinKey } }
        };

        var result = await _handler.Handle(request, default);

        result.Type.Should().Be(ResultType.Success);
        item.EndUtc.Should().Be(request.EndUtc);
        // Same target kept: no assignment churn, and never a whole-entity Update().
        _agendaRepo.Verify(r => r.RemoveAssignment(It.IsAny<AgendaAssignment>()), Times.Never);
        _agendaRepo.Verify(r => r.AddAssignment(It.IsAny<AgendaAssignment>()), Times.Never);
        _agendaRepo.Verify(r => r.Update(It.IsAny<AgendaItem>(), It.IsAny<CancellationToken>()), Times.Never);
        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);

        // Measured against the item as it was before the request was written over it: only the end moved.
        var entry = _journal.Entries.Should().ContainSingle().Subject;
        entry.Action.Should().Be(AuditActions.Agenda.Changed);
        entry.SubjectKey.Should().Be(item.AgendaItemKey);
        entry.KurinKey.Should().Be(_kurinKey);
        entry.Changes.Should().Equal(new AuditChange("end", null, "2026-08-14"));
    }

    [Fact]
    public async Task Handle_WhenTargetsChange_RemovesGoneAddsNew()
    {
        var group = Guid.NewGuid();
        _groups.Add(new Group("Соколи", _kurinKey) { GroupKey = group });
        var keptKurin = new AgendaAssignment { TargetType = AgendaTargetType.Kurin, TargetKey = _kurinKey };
        var removedGroup = new AgendaAssignment { TargetType = AgendaTargetType.Group, TargetKey = group };
        var item = new AgendaItem
        {
            AgendaItemKey = Guid.NewGuid(),
            KurinKey = _kurinKey,
            Kind = AgendaItemKind.Task,
            Title = "Задача",
            Assignments = new List<AgendaAssignment> { keptKurin, removedGroup }
        };
        _agendaRepo.Setup(r => r.GetByKeyWithAssignmentsAsync(item.AgendaItemKey, It.IsAny<CancellationToken>())).ReturnsAsync(item);

        var newMember = Guid.NewGuid();
        var request = new UpdateAgendaItemCommand
        {
            AgendaItemKey = item.AgendaItemKey,
            Kind = AgendaItemKind.Task,
            Title = "Задача",
            Targets = new List<AgendaTargetInput>
            {
                new() { TargetType = AgendaTargetType.Kurin, TargetKey = _kurinKey },
                new() { TargetType = AgendaTargetType.Member, TargetKey = newMember }
            }
        };

        var result = await _handler.Handle(request, default);

        result.Type.Should().Be(ResultType.Success);
        // Gone target deleted, new target inserted, kept target untouched.
        _agendaRepo.Verify(r => r.RemoveAssignment(removedGroup), Times.Once);
        _agendaRepo.Verify(r => r.RemoveAssignment(keptKurin), Times.Never);
        _agendaRepo.Verify(r => r.AddAssignment(It.Is<AgendaAssignment>(a => a.TargetType == AgendaTargetType.Member && a.TargetKey == newMember)), Times.Once);
        _agendaRepo.Verify(r => r.AddAssignment(It.Is<AgendaAssignment>(a => a.TargetType == AgendaTargetType.Kurin)), Times.Never);

        // Someone the directory cannot name still shows up as a person.
        _journal.Entries.Single().Changes.Should().Equal(
            new AuditChange("targets", "Весь курінь, Соколи", "Весь курінь, Людина"));
    }

    // Switching «кожному окремо» to a shared mode after someone moved their part would silently drop it.
    [Fact]
    public async Task Handle_WhenTheModeOfAMovedTargetChanges_ReturnsConflict()
    {
        var groupKey = Guid.NewGuid();
        var target = new AgendaAssignment { TargetType = AgendaTargetType.Group, TargetKey = groupKey, CompletionMode = AgendaCompletionMode.PerMember };
        target.Progress.Add(new AgendaAssignmentProgress { MemberKey = Guid.NewGuid(), Status = AgendaItemStatus.Done });
        var item = new AgendaItem
        {
            AgendaItemKey = Guid.NewGuid(),
            KurinKey = _kurinKey,
            Kind = AgendaItemKind.Task,
            Title = "Здати вкладку",
            Assignments = new List<AgendaAssignment> { target }
        };
        _agendaRepo.Setup(r => r.GetByKeyWithAssignmentsAsync(item.AgendaItemKey, It.IsAny<CancellationToken>())).ReturnsAsync(item);

        var result = await _handler.Handle(new UpdateAgendaItemCommand
        {
            AgendaItemKey = item.AgendaItemKey,
            Kind = AgendaItemKind.Task,
            Title = "Здати вкладку",
            Targets = [new() { TargetType = AgendaTargetType.Group, TargetKey = groupKey, CompletionMode = AgendaCompletionMode.Shared }]
        }, default);

        result.Type.Should().Be(ResultType.Conflict);
        target.CompletionMode.Should().Be(AgendaCompletionMode.PerMember);
        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhileNothingHasMoved_TheModeMayChange()
    {
        var groupKey = Guid.NewGuid();
        var target = new AgendaAssignment { TargetType = AgendaTargetType.Group, TargetKey = groupKey };
        var item = new AgendaItem
        {
            AgendaItemKey = Guid.NewGuid(),
            KurinKey = _kurinKey,
            Kind = AgendaItemKind.Task,
            Title = "Здати вкладку",
            Assignments = new List<AgendaAssignment> { target }
        };
        _agendaRepo.Setup(r => r.GetByKeyWithAssignmentsAsync(item.AgendaItemKey, It.IsAny<CancellationToken>())).ReturnsAsync(item);

        var result = await _handler.Handle(new UpdateAgendaItemCommand
        {
            AgendaItemKey = item.AgendaItemKey,
            Kind = AgendaItemKind.Task,
            Title = "Здати вкладку",
            Location = "  домівка  ",
            Targets = [new() { TargetType = AgendaTargetType.Group, TargetKey = groupKey, CompletionMode = AgendaCompletionMode.PerMember }]
        }, default);

        result.Type.Should().Be(ResultType.Success);
        target.CompletionMode.Should().Be(AgendaCompletionMode.PerMember);
        item.Location.Should().Be("домівка");
    }

    // The calendar's drag re-states every target without a mode; that must not reshape a task.
    [Fact]
    public async Task Handle_WhenATargetComesWithoutAMode_ItKeepsTheOneItHas()
    {
        var groupKey = Guid.NewGuid();
        var target = new AgendaAssignment { TargetType = AgendaTargetType.Group, TargetKey = groupKey, CompletionMode = AgendaCompletionMode.PerMember };
        target.Progress.Add(new AgendaAssignmentProgress { MemberKey = Guid.NewGuid(), Status = AgendaItemStatus.Done });
        var item = new AgendaItem
        {
            AgendaItemKey = Guid.NewGuid(),
            KurinKey = _kurinKey,
            Kind = AgendaItemKind.Task,
            Title = "Здати вкладку",
            Assignments = new List<AgendaAssignment> { target }
        };
        _agendaRepo.Setup(r => r.GetByKeyWithAssignmentsAsync(item.AgendaItemKey, It.IsAny<CancellationToken>())).ReturnsAsync(item);

        var result = await _handler.Handle(new UpdateAgendaItemCommand
        {
            AgendaItemKey = item.AgendaItemKey,
            Kind = AgendaItemKind.Task,
            Title = "Здати вкладку до п'ятниці",
            Targets = [new() { TargetType = AgendaTargetType.Group, TargetKey = groupKey }]
        }, default);

        result.Type.Should().Be(ResultType.Success);
        target.CompletionMode.Should().Be(AgendaCompletionMode.PerMember);
    }
}
