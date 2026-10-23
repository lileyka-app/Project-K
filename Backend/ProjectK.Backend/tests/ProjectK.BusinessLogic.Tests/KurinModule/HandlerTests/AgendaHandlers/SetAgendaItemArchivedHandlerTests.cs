using FluentAssertions;
using Moq;
using ProjectK.BusinessLogic.Modules.KurinModule.Features.Agenda.Archive;
using ProjectK.BusinessLogic.Modules.KurinModule.Services;
using ProjectK.BusinessLogic.Tests.TestHelpers;
using ProjectK.Common.Entities.KurinModule.Agenda;
using ProjectK.Common.Models.Audit;
using ProjectK.Common.Interfaces;
using ProjectK.Common.Interfaces.Modules.InfrastructureModule;
using ProjectK.Common.Interfaces.Modules.KurinModule;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Records;
using Xunit;

namespace ProjectK.BusinessLogic.Tests.KurinModule.HandlerTests.AgendaHandlers;

public class SetAgendaItemArchivedHandlerTests
{
    private readonly Guid _kurinKey = Guid.NewGuid();
    private readonly Guid _author = Guid.NewGuid();
    private readonly Mock<IAgendaItemRepository> _agenda = new();
    private readonly Mock<IUnitOfWork> _uow = new();
    private readonly Mock<IAgendaAccess> _access = new();
    private readonly Mock<ICurrentUserContext> _currentUser = new();
    private readonly SetAgendaItemArchivedCommandHandler _handler;
    private readonly JournalRecorder _journal;

    public SetAgendaItemArchivedHandlerTests()
    {
        _uow.SetupGet(u => u.AgendaItems).Returns(_agenda.Object);
        _currentUser.SetupGet(c => c.KurinKey).Returns(_kurinKey);
        _journal = new JournalRecorder(_currentUser.Object);
        _handler = new SetAgendaItemArchivedCommandHandler(_uow.Object, _access.Object, _currentUser.Object, TimeProvider.System, _journal.Audit);
    }

    private AgendaItem Item(AgendaItemKind kind = AgendaItemKind.Task)
    {
        var item = new AgendaItem { AgendaItemKey = Guid.NewGuid(), KurinKey = _kurinKey, Kind = kind, Title = "Прибрати", CreatedByUserKey = _author };
        _agenda.Setup(r => r.GetByKeyWithAssignmentsAsync(item.AgendaItemKey, It.IsAny<CancellationToken>())).ReturnsAsync(item);
        return item;
    }

    private void Viewer(Guid userKey) => _access.Setup(a => a.BuildViewerAsync(_kurinKey, It.IsAny<CancellationToken>()))
        .ReturnsAsync(new AgendaViewerContext(_kurinKey, userKey, null, null, [], [], false, false));

    [Fact]
    public async Task TheAuthor_ArchivesATask_AndTheArchiveRemembersWho()
    {
        var item = Item();
        Viewer(_author);

        (await _handler.Handle(new SetAgendaItemArchivedCommand(item.AgendaItemKey, true), default)).Type.Should().Be(ResultType.Success);

        item.ArchivedAtUtc.Should().NotBeNull();
        item.ArchivedByUserKey.Should().Be(_author);

        var entry = _journal.Entries.Should().ContainSingle().Subject;
        entry.Action.Should().Be(AuditActions.Agenda.Archived);
        entry.SubjectKey.Should().Be(item.AgendaItemKey);
        entry.SubjectLabel.Should().Be("Прибрати");

        await _handler.Handle(new SetAgendaItemArchivedCommand(item.AgendaItemKey, true), default);
        _journal.Entries.Should().ContainSingle("archiving it again changes nothing");
    }

    // Taken back, a long-done task would otherwise be swept into the archive again the same night.
    [Fact]
    public async Task ATaskTakenBack_CountsItsDoneTimeFromNow()
    {
        var item = Item();
        item.Status = AgendaItemStatus.Done;
        item.CompletedAtUtc = DateTime.UtcNow.AddDays(-90);
        item.ArchivedAtUtc = DateTime.UtcNow.AddDays(-60);
        Viewer(_author);

        await _handler.Handle(new SetAgendaItemArchivedCommand(item.AgendaItemKey, false), default);

        item.ArchivedAtUtc.Should().BeNull();
        item.CompletedAtUtc.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(1));
        _journal.Entries.Single().Action.Should().Be(AuditActions.Agenda.Restored);
    }

    [Fact]
    public async Task SomeoneWhoCannotEditIt_CannotArchiveIt()
    {
        var item = Item();
        Viewer(Guid.NewGuid());

        (await _handler.Handle(new SetAgendaItemArchivedCommand(item.AgendaItemKey, true), default)).Type.Should().Be(ResultType.Forbidden);
        item.ArchivedAtUtc.Should().BeNull();
    }

    [Fact]
    public async Task AnEvent_IsNotArchived()
    {
        var item = Item(AgendaItemKind.Event);
        Viewer(_author);

        (await _handler.Handle(new SetAgendaItemArchivedCommand(item.AgendaItemKey, true), default)).Type.Should().Be(ResultType.BadRequest);
    }
}
