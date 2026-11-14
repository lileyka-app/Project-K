using FluentAssertions;
using ProjectK.BusinessLogic.Modules.KurinModule.Services;
using ProjectK.Common.Entities.KurinModule.Agenda;
using ProjectK.Common.Enums.KurinModule;

namespace ProjectK.BusinessLogic.Tests.KurinModule.HandlerTests.AgendaHandlers;

public class AgendaOccurrencesTests
{
    // A Monday 16:00.
    private static readonly DateTime SeriesStart = new(2026, 10, 5, 16, 0, 0, DateTimeKind.Utc);

    private static AgendaItem OneOff() => new()
    {
        Kind = AgendaItemKind.Event,
        StartUtc = DateTime.SpecifyKind(SeriesStart, DateTimeKind.Unspecified),
        RecurrenceFrequency = RecurrenceFrequency.None
    };

    private static AgendaItem Weekly() => new()
    {
        Kind = AgendaItemKind.Event,
        StartUtc = DateTime.SpecifyKind(SeriesStart, DateTimeKind.Unspecified),
        EndUtc = DateTime.SpecifyKind(SeriesStart.AddHours(2), DateTimeKind.Unspecified),
        RecurrenceFrequency = RecurrenceFrequency.Weekly,
        RecurrenceInterval = 1,
        RecurrenceByWeekday = 1 << (int)DayOfWeek.Monday
    };

    [Fact]
    public void OneOff_WithoutOccurrence_ResolvesToNull()
    {
        AgendaOccurrences.TryResolveKey(OneOff(), null, TimeZoneInfo.Utc, out var key).Should().BeTrue();
        key.Should().BeNull();
    }

    [Fact]
    public void OneOff_NamingItsOwnStart_ResolvesToNull()
    {
        AgendaOccurrences.TryResolveKey(OneOff(), SeriesStart, TimeZoneInfo.Utc, out var key).Should().BeTrue();
        key.Should().BeNull();
    }

    [Fact]
    public void OneOff_NamingAnotherDay_IsRefused()
    {
        AgendaOccurrences.TryResolveKey(OneOff(), SeriesStart.AddDays(1), TimeZoneInfo.Utc, out _).Should().BeFalse();
    }

    [Fact]
    public void Series_WithoutOccurrence_IsRefused()
    {
        AgendaOccurrences.TryResolveKey(Weekly(), null, TimeZoneInfo.Utc, out _).Should().BeFalse();
    }

    [Fact]
    public void Series_OnARealOccurrence_ResolvesToThatStart()
    {
        var thirdMonday = SeriesStart.AddDays(14);

        AgendaOccurrences.TryResolveKey(Weekly(), thirdMonday, TimeZoneInfo.Utc, out var key).Should().BeTrue();

        key.Should().Be(thirdMonday);
        key!.Value.Kind.Should().Be(DateTimeKind.Utc);
    }

    [Fact]
    public void Series_OnADayItDoesNotMeet_IsRefused()
    {
        AgendaOccurrences.TryResolveKey(Weekly(), SeriesStart.AddDays(1), TimeZoneInfo.Utc, out _).Should().BeFalse();
        AgendaOccurrences.TryResolveKey(Weekly(), SeriesStart.AddDays(7).AddMinutes(30), TimeZoneInfo.Utc, out _).Should().BeFalse();
        AgendaOccurrences.TryResolveKey(Weekly(), SeriesStart.AddDays(-7), TimeZoneInfo.Utc, out _).Should().BeFalse();
    }

    [Fact]
    public void Series_PastItsEnd_IsRefused()
    {
        var item = Weekly();
        item.RecurrenceEndUtc = SeriesStart.AddDays(7);

        AgendaOccurrences.TryResolveKey(item, SeriesStart.AddDays(7), TimeZoneInfo.Utc, out _).Should().BeTrue();
        AgendaOccurrences.TryResolveKey(item, SeriesStart.AddDays(14), TimeZoneInfo.Utc, out _).Should().BeFalse();
    }

    [Fact]
    public void KeyOf_IsNullForOneOff_AndTheStartForSeries()
    {
        AgendaOccurrences.KeyOf(OneOff(), SeriesStart).Should().BeNull();
        AgendaOccurrences.KeyOf(Weekly(), SeriesStart.AddDays(7)).Should().Be(SeriesStart.AddDays(7));
    }
}
