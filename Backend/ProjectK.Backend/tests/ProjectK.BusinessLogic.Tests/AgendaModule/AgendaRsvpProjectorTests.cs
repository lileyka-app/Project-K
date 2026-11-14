using FluentAssertions;
using ProjectK.BusinessLogic.Modules.AgendaModule.Services;
using ProjectK.Common.Entities.AgendaModule;
using ProjectK.Common.Enums.AgendaModule;

namespace ProjectK.BusinessLogic.Tests.AgendaModule;

public class AgendaRsvpProjectorTests
{
    private readonly Guid _item = Guid.NewGuid();
    private static readonly DateTime Base = new(2026, 8, 1, 10, 0, 0, DateTimeKind.Utc);

    private AgendaResponse Going(int minute, Guid? user = null) =>
        new() { AgendaItemKey = _item, UserKey = user ?? Guid.NewGuid(), Status = AgendaRsvpStatus.Going, RespondedAtUtc = Base.AddMinutes(minute) };

    private AgendaResponse With(AgendaRsvpStatus status, int minute) =>
        new() { AgendaItemKey = _item, UserKey = Guid.NewGuid(), Status = status, RespondedAtUtc = Base.AddMinutes(minute) };

    [Fact]
    public void Project_WithinCapacity_ConfirmsAllGoing()
    {
        var rows = new List<AgendaResponse> { Going(1), Going(2) };

        var result = AgendaRsvpRules.Project(_item, null, rows, capacity: 5, waitlistEnabled: true, new Dictionary<Guid, string>(), null);

        result.GoingConfirmedCount.Should().Be(2);
        result.GoingWaitlistCount.Should().Be(0);
        result.Responses.Should().OnlyContain(r => !r.IsWaitlisted);
    }

    [Fact]
    public void Project_OverCapacity_WaitlistsLatestByTime()
    {
        var first = Going(1);
        var second = Going(2);
        var third = Going(3);
        var rows = new List<AgendaResponse> { third, first, second }; // unordered on purpose

        var result = AgendaRsvpRules.Project(_item, null, rows, capacity: 2, waitlistEnabled: true, new Dictionary<Guid, string>(), null);

        result.GoingConfirmedCount.Should().Be(2);
        result.GoingWaitlistCount.Should().Be(1);
        // The third-earliest RSVP is the one waitlisted.
        result.Responses.Single(r => r.IsWaitlisted).UserKey.Should().Be(third.UserKey);
    }

    [Fact]
    public void Project_OverCapacityButWaitlistDisabled_ConfirmsAllAndReportsNoQueue()
    {
        var rows = new List<AgendaResponse> { Going(1), Going(2), Going(3) };

        var result = AgendaRsvpRules.Project(_item, null, rows, capacity: 2, waitlistEnabled: false, new Dictionary<Guid, string>(), null);

        // Capacity without a waitlist is advisory: everyone is confirmed and the queue count stays 0.
        result.GoingConfirmedCount.Should().Be(3);
        result.GoingWaitlistCount.Should().Be(0);
        result.Responses.Should().OnlyContain(r => !r.IsWaitlisted);
    }

    [Fact]
    public void Project_NoCapacity_ConfirmsEveryGoing()
    {
        var rows = new List<AgendaResponse> { Going(1), Going(2), Going(3) };

        var result = AgendaRsvpRules.Project(_item, null, rows, capacity: null, waitlistEnabled: true, new Dictionary<Guid, string>(), null);

        result.GoingConfirmedCount.Should().Be(3);
        result.GoingWaitlistCount.Should().Be(0);
    }

    [Fact]
    public void Project_CountsAndMyStatus_AreResolved()
    {
        var me = Guid.NewGuid();
        var rows = new List<AgendaResponse>
        {
            Going(1, me),
            With(AgendaRsvpStatus.NotGoing, 2),
            With(AgendaRsvpStatus.Maybe, 3),
            With(AgendaRsvpStatus.Maybe, 4)
        };

        var result = AgendaRsvpRules.Project(_item, null, rows, capacity: null, waitlistEnabled: false, new Dictionary<Guid, string>(), me);

        result.NotGoingCount.Should().Be(1);
        result.MaybeCount.Should().Be(2);
        result.MyStatus.Should().Be(AgendaRsvpStatus.Going);
        result.Responses.Should().HaveCount(4);
    }
}
