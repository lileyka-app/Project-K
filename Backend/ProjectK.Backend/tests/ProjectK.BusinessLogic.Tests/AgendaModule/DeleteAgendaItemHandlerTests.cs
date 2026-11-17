using FluentAssertions;
using Moq;
using ProjectK.BusinessLogic.Modules.AgendaModule.Features.Agenda;
using ProjectK.BusinessLogic.Modules.AgendaModule.Services;
using ProjectK.BusinessLogic.Modules.KurinModule.Services;
using ProjectK.BusinessLogic.Modules.MemberModule.Services;
using ProjectK.BusinessLogic.Tests.TestHelpers;
using ProjectK.Common.Contracts.AgendaModule;
using ProjectK.Common.Contracts.KurinModule;
using ProjectK.Common.Contracts.MemberModule;
using ProjectK.Common.Contracts.NotificationsModule;
using ProjectK.Common.Contracts.Shared;
using ProjectK.Common.Dtos.AuditModule;
using ProjectK.Common.Entities.AgendaModule;
using ProjectK.Common.Enums.AgendaModule;
using ProjectK.Common.Enums.Shared;
using ProjectK.Common.Records.AgendaModule;
using ProjectK.Common.Records.KurinModule;
using ProjectK.Common.Records.MemberModule;
using ProjectK.Common.Rules.AuditModule;

namespace ProjectK.BusinessLogic.Tests.AgendaModule;

public class DeleteAgendaItemHandlerTests
{
    private readonly Mock<IUnitOfWork> _uow = new();
    private readonly Mock<IMemberDirectory> _memberDirectory = new();
    private readonly Mock<IAgendaAccess> _access = new();
    private readonly Mock<ICurrentUserContext> _currentUser = new();
    private readonly Mock<IDomainEventPublisher> _events = new();
    private readonly Mock<IAgendaItemRepository> _agendaRepo = new();
    private readonly Mock<IMemberRepository> _memberRepo = new();
    private readonly DeleteAgendaItemCommandHandler _handler;
    private readonly JournalRecorder _journal;

    private readonly Guid _kurinKey = Guid.NewGuid();
    private readonly Guid _creatorKey = Guid.NewGuid();

    public DeleteAgendaItemHandlerTests()
    {
        _uow.Setup(u => u.AgendaItems).Returns(_agendaRepo.Object);
        _memberDirectory.Setup(r => r.GetByKurinAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<MemberSummary>());
        _memberDirectory.Setup(r => r.GetByKurinAsync(It.IsAny<Guid>(), It.IsAny<MemberSelection>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<MemberSummary>());
        _currentUser.Setup(c => c.KurinKey).Returns(_kurinKey);
        _journal = new JournalRecorder(_currentUser.Object);
        _handler = new DeleteAgendaItemCommandHandler(_uow.Object, _memberDirectory.Object, new OfficeDirectory(_uow.Object), KurinDirectories.Over(_uow.Object), KurinDirectories.TagsOver(_uow.Object), _access.Object, _currentUser.Object, _events.Object, _journal.Audit);
    }

    private AgendaItem Item() => new()
    {
        AgendaItemKey = Guid.NewGuid(),
        KurinKey = _kurinKey,
        Kind = AgendaItemKind.Event,
        Title = "Подія",
        Location = "Пласт-дім",
        CreatedByUserKey = _creatorKey,
        Assignments = new List<AgendaAssignment> { new() { TargetType = AgendaTargetType.Kurin, TargetKey = _kurinKey } }
    };

    private void SetupViewer(bool canSeeWholeKurin, bool isLeadership, Guid? viewerUserKey)
    {
        _access.Setup(a => a.BuildViewerAsync(_kurinKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AgendaViewerContext(
                _kurinKey, viewerUserKey, null, null, Array.Empty<Guid>(), Array.Empty<Guid>(), canSeeWholeKurin, isLeadership));
    }

    [Fact]
    public async Task Handle_WhenItemMissing_ReturnsNotFound()
    {
        _agendaRepo.Setup(r => r.GetByKeyWithAssignmentsAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((AgendaItem?)null);

        var result = await _handler.Handle(new DeleteAgendaItemCommand(Guid.NewGuid()), default);

        result.Type.Should().Be(ResultType.NotFound);
    }

    [Fact]
    public async Task Handle_WhenPlainMemberNotCreator_ReturnsForbidden()
    {
        var item = Item();
        _agendaRepo.Setup(r => r.GetByKeyWithAssignmentsAsync(item.AgendaItemKey, It.IsAny<CancellationToken>())).ReturnsAsync(item);
        SetupViewer(canSeeWholeKurin: false, isLeadership: false, viewerUserKey: Guid.NewGuid());

        var result = await _handler.Handle(new DeleteAgendaItemCommand(item.AgendaItemKey), default);

        result.Type.Should().Be(ResultType.Forbidden);
        _agendaRepo.Verify(r => r.Delete(It.IsAny<AgendaItem>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenCreator_DeletesAndSaves()
    {
        var item = Item();
        _agendaRepo.Setup(r => r.GetByKeyWithAssignmentsAsync(item.AgendaItemKey, It.IsAny<CancellationToken>())).ReturnsAsync(item);
        SetupViewer(canSeeWholeKurin: false, isLeadership: false, viewerUserKey: _creatorKey);

        var result = await _handler.Handle(new DeleteAgendaItemCommand(item.AgendaItemKey), default);

        result.Type.Should().Be(ResultType.Success);
        _agendaRepo.Verify(r => r.Delete(item, It.IsAny<CancellationToken>()), Times.Once);
        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);

        var entry = _journal.Entries.Should().ContainSingle().Subject;
        entry.Action.Should().Be(AuditActionNames.Agenda.Deleted);
        entry.SubjectKey.Should().Be(item.AgendaItemKey);
        entry.SubjectLabel.Should().Be("Подія");
        entry.Changes.Should().Equal(
            new AuditChange("title", "Подія", null),
            new AuditChange("kind", "Event", null),
            new AuditChange("location", "Пласт-дім", null),
            new AuditChange("targets", "Весь курінь", null));
    }
}
