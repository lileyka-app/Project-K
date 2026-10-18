using FluentAssertions;
using ProjectK.BusinessLogic.Modules.ScoreModule.Services;
using ProjectK.Common.Entities.ScoreModule;
using Xunit;

namespace ProjectK.BusinessLogic.Tests.ScoreModule;

/// <summary>What a mark is worth: the one rule shared by the whole book and the attendance-only read.</summary>
public class ScoreAttendanceBookTests
{
    private static readonly Guid Shodyny = Guid.NewGuid();
    private static readonly Guid Tabir = Guid.NewGuid();
    private static readonly Guid Event = Guid.NewGuid();

    private static ScoreAttendanceBook Book(params ScoreAttendanceRate[] rates) => new(rates, []);

    [Fact]
    public void AttendancePoints_TakesTheEventsOwnRate_OverItsGroups()
    {
        var book = Book(
            new ScoreAttendanceRate { AgendaCategoryKey = Shodyny, Points = 2 },
            new ScoreAttendanceRate { AgendaItemKey = Event, Points = 5 });

        book.AttendancePoints(Event, Shodyny).Should().Be(5);
    }

    [Fact]
    public void AttendancePoints_FallsBackToTheGroupsRate()
    {
        var book = Book(new ScoreAttendanceRate { AgendaCategoryKey = Shodyny, Points = 2 });

        book.AttendancePoints(Event, Shodyny).Should().Be(2);
    }

    [Fact]
    public void AttendancePoints_IsZero_WithoutARate()
    {
        var book = Book(new ScoreAttendanceRate { AgendaCategoryKey = Tabir, Points = 3 });

        book.AttendancePoints(Event, Shodyny).Should().Be(0);
        book.AttendancePoints(Event, null).Should().Be(0);
    }
}
