using FluentAssertions;
using Moq;
using ProjectK.BusinessLogic.Modules.KurinModule.Services;
using ProjectK.Common.Contracts.KurinModule;
using ProjectK.Common.Contracts.Shared;
using ProjectK.Common.Entities.KurinModule.Agenda;
using ProjectK.Common.Enums.KurinModule;

namespace ProjectK.BusinessLogic.Tests.KurinModule.HandlerTests.AgendaHandlers;

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
        await AgendaRsvpWriter.UpsertAsync(_uow.Object, _item, _user, Monday, AgendaRsvpStatus.Going, Now, default);

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
        await AgendaRsvpWriter.UpsertAsync(_uow.Object, _item, _user, Monday, AgendaRsvpStatus.Going, Now, default);
        await AgendaRsvpWriter.UpsertAsync(_uow.Object, _item, _user, Monday.AddDays(7), AgendaRsvpStatus.NotGoing, Now.AddMinutes(1), default);

        _rows.Should().HaveCount(2);
        _rows.Single(r => r.OccurrenceStartUtc == Monday).Status.Should().Be(AgendaRsvpStatus.Going);
        _rows.Single(r => r.OccurrenceStartUtc == Monday.AddDays(7)).Status.Should().Be(AgendaRsvpStatus.NotGoing);
        _responses.Verify(r => r.Update(It.IsAny<AgendaResponse>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ChangingTheAnswer_OnTheSameOccurrence_RetimesThatRowOnly()
    {
        await AgendaRsvpWriter.UpsertAsync(_uow.Object, _item, _user, Monday, AgendaRsvpStatus.Maybe, Now, default);
        await AgendaRsvpWriter.UpsertAsync(_uow.Object, _item, _user, Monday.AddDays(7), AgendaRsvpStatus.Maybe, Now, default);

        await AgendaRsvpWriter.UpsertAsync(_uow.Object, _item, _user, Monday, AgendaRsvpStatus.Going, Now.AddHours(1), default);

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
        await AgendaRsvpWriter.UpsertAsync(_uow.Object, _item, _user, Monday, AgendaRsvpStatus.Going, Now, default);

        await AgendaRsvpWriter.UpsertAsync(_uow.Object, _item, _user, Monday, AgendaRsvpStatus.Going, Now.AddHours(1), default);

        _rows.Should().ContainSingle().Which.RespondedAtUtc.Should().Be(Now);
        _responses.Verify(r => r.Update(It.IsAny<AgendaResponse>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task OneOffEvent_IsKeyedByNull()
    {
        await AgendaRsvpWriter.UpsertAsync(_uow.Object, _item, _user, null, AgendaRsvpStatus.Going, Now, default);
        await AgendaRsvpWriter.UpsertAsync(_uow.Object, _item, _user, null, AgendaRsvpStatus.NotGoing, Now.AddHours(1), default);

        _rows.Should().ContainSingle().Which.Should().BeEquivalentTo(new { OccurrenceStartUtc = (DateTime?)null, Status = AgendaRsvpStatus.NotGoing });
    }
}
