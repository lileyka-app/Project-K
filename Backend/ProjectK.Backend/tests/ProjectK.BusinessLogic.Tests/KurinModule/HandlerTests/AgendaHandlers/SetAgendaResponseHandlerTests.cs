using FluentAssertions;
using Moq;
using ProjectK.BusinessLogic.Modules.KurinModule.Features.Agenda;
using ProjectK.BusinessLogic.Modules.KurinModule.Services;
using ProjectK.Common.Contracts.InfrastructureModule;
using ProjectK.Common.Contracts.KraiModule;
using ProjectK.Common.Contracts.KurinModule;
using ProjectK.Common.Contracts.MemberModule;
using ProjectK.Common.Contracts.Shared;
using ProjectK.Common.Entities.KurinModule.Agenda;
using ProjectK.Common.Enums.KurinModule;
using ProjectK.Common.Enums.Shared;
using ProjectK.Common.Records.MemberModule;

namespace ProjectK.BusinessLogic.Tests.KurinModule.HandlerTests.AgendaHandlers;

public class SetAgendaResponseHandlerTests
{
    private readonly Mock<IUnitOfWork> _uow = new();
    private readonly Mock<IMemberDirectory> _memberDirectory = new();
    private readonly Mock<IAgendaAccess> _access = new();
    private readonly Mock<ICurrentUserContext> _currentUser = new();
    private readonly Mock<IAgendaItemRepository> _items = new();
    private readonly Mock<IAgendaResponseRepository> _responses = new();
    private readonly Mock<IMemberRepository> _members = new();
    private readonly Mock<IAgendaCategoryRepository> _categories = new();
    private readonly SetAgendaResponseCommandHandler _handler;

    private readonly Guid _kurinKey = Guid.NewGuid();
    private readonly Guid _userKey = Guid.NewGuid();

    public SetAgendaResponseHandlerTests()
    {
        _uow.Setup(u => u.AgendaItems).Returns(_items.Object);
        _uow.Setup(u => u.AgendaResponses).Returns(_responses.Object);
        _uow.Setup(u => u.AgendaCategories).Returns(_categories.Object);
        _currentUser.Setup(c => c.UserId).Returns(_userKey);
        _currentUser.Setup(c => c.KurinKey).Returns(_kurinKey);
        _memberDirectory.Setup(m => m.GetByKurinAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(new List<MemberSummary>());
        _memberDirectory.Setup(m => m.GetByKurinAsync(It.IsAny<Guid>(), It.IsAny<MemberSelection>(), It.IsAny<CancellationToken>())).ReturnsAsync(new List<MemberSummary>());
        _responses.Setup(r => r.GetForItemAsync(It.IsAny<Guid>(), It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<AgendaResponse>());
        _handler = new SetAgendaResponseCommandHandler(_uow.Object, _memberDirectory.Object, _access.Object, _currentUser.Object, Mock.Of<IKraiProfileDirectory>(), new AgendaRsvpWriter(_uow.Object));
    }

    private AgendaItem Event(Guid? kurin = null) => new()
    {
        AgendaItemKey = Guid.NewGuid(),
        KurinKey = kurin ?? _kurinKey,
        Kind = AgendaItemKind.Event,
        Assignments = new List<AgendaAssignment> { new() { TargetType = AgendaTargetType.Kurin, TargetKey = kurin ?? _kurinKey } }
    };

    private void SetupVisible(Guid kurinKey, bool visible)
    {
        _access.Setup(a => a.BuildViewerAsync(kurinKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AgendaViewerContext(kurinKey, _userKey, null, null,
                Array.Empty<Guid>(), Array.Empty<Guid>(), CanSeeWholeKurin: visible, IsLeadership: visible));
    }

    [Fact]
    public async Task Handle_OnTask_ReturnsBadRequest()
    {
        var item = Event();
        item.Kind = AgendaItemKind.Task;
        _items.Setup(r => r.GetByKeyWithAssignmentsAsync(item.AgendaItemKey, It.IsAny<CancellationToken>())).ReturnsAsync(item);

        var result = await _handler.Handle(new SetAgendaResponseCommand(item.AgendaItemKey, null, AgendaRsvpStatus.Going), default);

        result.Type.Should().Be(ResultType.BadRequest);
    }

    [Fact]
    public async Task Handle_ForEventInAnotherKurin_ReturnsForbidden()
    {
        var item = Event(kurin: Guid.NewGuid()); // belongs to a different kurin than the caller's claim
        _items.Setup(r => r.GetByKeyWithAssignmentsAsync(item.AgendaItemKey, It.IsAny<CancellationToken>())).ReturnsAsync(item);

        var result = await _handler.Handle(new SetAgendaResponseCommand(item.AgendaItemKey, null, AgendaRsvpStatus.Going), default);

        result.Type.Should().Be(ResultType.Forbidden);
        _responses.Verify(r => r.Create(It.IsAny<AgendaResponse>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenNotVisible_ReturnsForbidden()
    {
        var item = Event();
        item.Assignments.Clear(); // no assignment reaches the viewer
        _items.Setup(r => r.GetByKeyWithAssignmentsAsync(item.AgendaItemKey, It.IsAny<CancellationToken>())).ReturnsAsync(item);
        SetupVisible(_kurinKey, visible: false);

        var result = await _handler.Handle(new SetAgendaResponseCommand(item.AgendaItemKey, null, AgendaRsvpStatus.Going), default);

        result.Type.Should().Be(ResultType.Forbidden);
    }

    [Fact]
    public async Task Handle_FirstResponse_CreatesIt()
    {
        var item = Event();
        _items.Setup(r => r.GetByKeyWithAssignmentsAsync(item.AgendaItemKey, It.IsAny<CancellationToken>())).ReturnsAsync(item);
        SetupVisible(_kurinKey, visible: true);
        _responses.Setup(r => r.GetForItemAndUserAsync(item.AgendaItemKey, _userKey, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync((AgendaResponse?)null);

        var result = await _handler.Handle(new SetAgendaResponseCommand(item.AgendaItemKey, null, AgendaRsvpStatus.Going), default);

        result.Type.Should().Be(ResultType.Success);
        _responses.Verify(r => r.Create(It.Is<AgendaResponse>(x => x.UserKey == _userKey && x.Status == AgendaRsvpStatus.Going), It.IsAny<CancellationToken>()), Times.Once);
        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ChangingStatus_RetimesTheResponse()
    {
        var item = Event();
        _items.Setup(r => r.GetByKeyWithAssignmentsAsync(item.AgendaItemKey, It.IsAny<CancellationToken>())).ReturnsAsync(item);
        SetupVisible(_kurinKey, visible: true);
        var existing = new AgendaResponse
        {
            AgendaItemKey = item.AgendaItemKey,
            UserKey = _userKey,
            Status = AgendaRsvpStatus.Maybe,
            RespondedAtUtc = new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc)
        };
        _responses.Setup(r => r.GetForItemAndUserAsync(item.AgendaItemKey, _userKey, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        var result = await _handler.Handle(new SetAgendaResponseCommand(item.AgendaItemKey, null, AgendaRsvpStatus.Going), default);

        result.Type.Should().Be(ResultType.Success);
        existing.Status.Should().Be(AgendaRsvpStatus.Going);
        existing.RespondedAtUtc.Should().BeAfter(new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc));
        _responses.Verify(r => r.Update(existing, It.IsAny<CancellationToken>()), Times.Once);
    }

    private AgendaItem WeeklyEvent()
    {
        var item = Event();
        item.StartUtc = new DateTime(2026, 10, 5, 16, 0, 0, DateTimeKind.Unspecified); // a Monday
        item.RecurrenceFrequency = RecurrenceFrequency.Weekly;
        item.RecurrenceByWeekday = 1 << (int)DayOfWeek.Monday;
        return item;
    }

    [Fact]
    public async Task Handle_OnASeriesOccurrence_WritesThatOccurrenceOnly()
    {
        var item = WeeklyEvent();
        var secondMonday = new DateTime(2026, 10, 12, 16, 0, 0, DateTimeKind.Utc);
        _items.Setup(r => r.GetByKeyWithAssignmentsAsync(item.AgendaItemKey, It.IsAny<CancellationToken>())).ReturnsAsync(item);
        SetupVisible(_kurinKey, visible: true);
        _responses.Setup(r => r.GetForItemAndUserAsync(item.AgendaItemKey, _userKey, secondMonday, It.IsAny<CancellationToken>()))
            .ReturnsAsync((AgendaResponse?)null);

        var result = await _handler.Handle(new SetAgendaResponseCommand(item.AgendaItemKey, secondMonday, AgendaRsvpStatus.Going), default);

        result.Type.Should().Be(ResultType.Success);
        result.Data!.OccurrenceStartUtc.Should().Be(secondMonday);
        _responses.Verify(r => r.Create(It.Is<AgendaResponse>(x => x.OccurrenceStartUtc == secondMonday && x.Status == AgendaRsvpStatus.Going), It.IsAny<CancellationToken>()), Times.Once);
        _responses.Verify(r => r.GetForItemAsync(item.AgendaItemKey, secondMonday, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_OnASeriesWithoutAnOccurrence_ReturnsBadRequest()
    {
        var item = WeeklyEvent();
        _items.Setup(r => r.GetByKeyWithAssignmentsAsync(item.AgendaItemKey, It.IsAny<CancellationToken>())).ReturnsAsync(item);
        SetupVisible(_kurinKey, visible: true);

        var result = await _handler.Handle(new SetAgendaResponseCommand(item.AgendaItemKey, null, AgendaRsvpStatus.Going), default);

        result.Type.Should().Be(ResultType.BadRequest);
        _responses.Verify(r => r.Create(It.IsAny<AgendaResponse>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_OnADayTheSeriesDoesNotMeet_ReturnsBadRequest()
    {
        var item = WeeklyEvent();
        _items.Setup(r => r.GetByKeyWithAssignmentsAsync(item.AgendaItemKey, It.IsAny<CancellationToken>())).ReturnsAsync(item);
        SetupVisible(_kurinKey, visible: true);
        var tuesday = new DateTime(2026, 10, 13, 16, 0, 0, DateTimeKind.Utc);

        var result = await _handler.Handle(new SetAgendaResponseCommand(item.AgendaItemKey, tuesday, AgendaRsvpStatus.Going), default);

        result.Type.Should().Be(ResultType.BadRequest);
        _responses.Verify(r => r.Create(It.IsAny<AgendaResponse>(), It.IsAny<CancellationToken>()), Times.Never);
        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}
