using FluentAssertions;
using Moq;
using ProjectK.Common.Contracts.KraiModule;
using ProjectK.BusinessLogic.Modules.AgendaModule.Services;
using ProjectK.Common.Contracts.AgendaModule;
using ProjectK.Common.Contracts.Shared;
using ProjectK.Common.Entities.AgendaModule;
using ProjectK.Common.Enums.AgendaModule;
using ProjectK.Common.Records.AgendaModule;

namespace ProjectK.BusinessLogic.Tests.AgendaModule;

public class AgendaRsvpWriterTests
{
    private static readonly DateTime Now = new(2026, 10, 9, 12, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime Monday = new(2026, 10, 12, 16, 0, 0, DateTimeKind.Utc);

    private readonly Mock<IUnitOfWork> _uow = new();
    private readonly Mock<IAgendaResponseRepository> _responses = new();
    private readonly List<AgendaResponse> _rows = [];
    private readonly AgendaItem _item = new() { AgendaItemKey = Guid.NewGuid(), Kind = AgendaItemKind.Event };
    private readonly Guid _user = Guid.NewGuid();

    public AgendaRsvpWriterTests()
    {
        _uow.Setup(u => u.AgendaResponses).Returns(_responses.Object);
        // The fake repository answers by the same (item, user, occurrence) key the real one does.
        _responses.Setup(r => r.GetForItemAndUserAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid item, Guid user, DateTime? occurrence, CancellationToken _) =>
                _rows.FirstOrDefault(x => x.AgendaItemKey == item && x.UserKey == user && x.OccurrenceStartUtc == occurrence));
        _responses.Setup(r => r.Create(It.IsAny<AgendaResponse>(), It.IsAny<CancellationToken>()))
            .Callback((AgendaResponse row, CancellationToken _) => _rows.Add(row));
    }

    [Fact]
    public async Task FirstAnswer_OnAnOccurrence_CreatesARowKeyedByIt()
    {
        await new AgendaRsvpWriter(_uow.Object, Mock.Of<IKraiProfileDirectory>()).UpsertAsync(_item.AgendaItemKey, _user, Monday, AgendaRsvpStatus.Going, Now, default);

        _rows.Should().ContainSingle().Which.Should().BeEquivalentTo(new
        {
            AgendaItemKey = _item.AgendaItemKey,
            UserKey = _user,
            OccurrenceStartUtc = (DateTime?)Monday,
            Status = AgendaRsvpStatus.Going,
            RespondedAtUtc = Now
        });
    }

    [Fact]
    public async Task AnswersOnTwoOccurrences_AreTwoRows_NotOneOverwritten()
    {
        await new AgendaRsvpWriter(_uow.Object, Mock.Of<IKraiProfileDirectory>()).UpsertAsync(_item.AgendaItemKey, _user, Monday, AgendaRsvpStatus.Going, Now, default);
        await new AgendaRsvpWriter(_uow.Object, Mock.Of<IKraiProfileDirectory>()).UpsertAsync(_item.AgendaItemKey, _user, Monday.AddDays(7), AgendaRsvpStatus.NotGoing, Now.AddMinutes(1), default);

        _rows.Should().HaveCount(2);
        _rows.Single(r => r.OccurrenceStartUtc == Monday).Status.Should().Be(AgendaRsvpStatus.Going);
        _rows.Single(r => r.OccurrenceStartUtc == Monday.AddDays(7)).Status.Should().Be(AgendaRsvpStatus.NotGoing);
        _responses.Verify(r => r.Update(It.IsAny<AgendaResponse>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ChangingTheAnswer_OnTheSameOccurrence_RetimesThatRowOnly()
    {
        await new AgendaRsvpWriter(_uow.Object, Mock.Of<IKraiProfileDirectory>()).UpsertAsync(_item.AgendaItemKey, _user, Monday, AgendaRsvpStatus.Maybe, Now, default);
        await new AgendaRsvpWriter(_uow.Object, Mock.Of<IKraiProfileDirectory>()).UpsertAsync(_item.AgendaItemKey, _user, Monday.AddDays(7), AgendaRsvpStatus.Maybe, Now, default);

        await new AgendaRsvpWriter(_uow.Object, Mock.Of<IKraiProfileDirectory>()).UpsertAsync(_item.AgendaItemKey, _user, Monday, AgendaRsvpStatus.Going, Now.AddHours(1), default);

        _rows.Should().HaveCount(2);
        var changed = _rows.Single(r => r.OccurrenceStartUtc == Monday);
        changed.Status.Should().Be(AgendaRsvpStatus.Going);
        changed.RespondedAtUtc.Should().Be(Now.AddHours(1));
        _rows.Single(r => r.OccurrenceStartUtc == Monday.AddDays(7)).Status.Should().Be(AgendaRsvpStatus.Maybe);
        _responses.Verify(r => r.Update(changed, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RepeatingTheSameAnswer_TouchesNothing()
    {
        await new AgendaRsvpWriter(_uow.Object, Mock.Of<IKraiProfileDirectory>()).UpsertAsync(_item.AgendaItemKey, _user, Monday, AgendaRsvpStatus.Going, Now, default);

        await new AgendaRsvpWriter(_uow.Object, Mock.Of<IKraiProfileDirectory>()).UpsertAsync(_item.AgendaItemKey, _user, Monday, AgendaRsvpStatus.Going, Now.AddHours(1), default);

        _rows.Should().ContainSingle().Which.RespondedAtUtc.Should().Be(Now);
        _responses.Verify(r => r.Update(It.IsAny<AgendaResponse>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task OneOffEvent_IsKeyedByNull()
    {
        await new AgendaRsvpWriter(_uow.Object, Mock.Of<IKraiProfileDirectory>()).UpsertAsync(_item.AgendaItemKey, _user, null, AgendaRsvpStatus.Going, Now, default);
        await new AgendaRsvpWriter(_uow.Object, Mock.Of<IKraiProfileDirectory>()).UpsertAsync(_item.AgendaItemKey, _user, null, AgendaRsvpStatus.NotGoing, Now.AddHours(1), default);

        _rows.Should().ContainSingle().Which.Should().BeEquivalentTo(new { OccurrenceStartUtc = (DateTime?)null, Status = AgendaRsvpStatus.NotGoing });
    }

    [Fact]
    public async Task AnAnswerFromTheDashboard_IsStaged_ForAnEventItsKurinShows()
    {
        var outcome = await Answer(Viewer(_item.KurinKey, wholeKurin: true));

        outcome.Should().Be(AgendaRsvpOutcome.Staged);
        _rows.Should().ContainSingle().Which.Should().BeEquivalentTo(new { _item.AgendaItemKey, UserKey = _user, OccurrenceStartUtc = (DateTime?)null });
    }

    [Fact]
    public async Task AnAnswerFromTheDashboard_IsRefused_WhereThePersonCannotSeeTheEvent()
    {
        (await Answer(Viewer(Guid.NewGuid(), wholeKurin: true))).Should().Be(AgendaRsvpOutcome.NotVisible);
        (await Answer(Viewer(_item.KurinKey, wholeKurin: false))).Should().Be(AgendaRsvpOutcome.NotVisible);
        _rows.Should().BeEmpty();
    }

    [Fact]
    public async Task AnAnswerFromTheDashboard_ToATask_FindsNoEvent()
    {
        _item.Kind = AgendaItemKind.Task;

        (await Answer(Viewer(_item.KurinKey, wholeKurin: true))).Should().Be(AgendaRsvpOutcome.NoSuchEvent);
        _rows.Should().BeEmpty();
    }

    private Task<AgendaRsvpOutcome> Answer(AgendaViewerContext viewer)
    {
        var items = new Mock<IAgendaItemRepository>();
        items.Setup(r => r.GetByKeyWithAssignmentsAsync(_item.AgendaItemKey, It.IsAny<CancellationToken>())).ReturnsAsync(_item);
        _uow.Setup(u => u.AgendaItems).Returns(items.Object);
        return new AgendaRsvpWriter(_uow.Object, Mock.Of<IKraiProfileDirectory>())
            .AnswerAsync([viewer], _item.AgendaItemKey, null, AgendaRsvpStatus.Going, Now, default);
    }

    private AgendaViewerContext Viewer(Guid kurinKey, bool wholeKurin) =>
        new(kurinKey, _user, Guid.NewGuid(), null, new HashSet<Guid>(), [], CanSeeWholeKurin: wholeKurin, IsLeadership: wholeKurin);
}
