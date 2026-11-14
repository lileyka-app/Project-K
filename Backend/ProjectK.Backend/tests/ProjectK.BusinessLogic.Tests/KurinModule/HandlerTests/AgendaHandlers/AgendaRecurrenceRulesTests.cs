using FluentAssertions;
using ProjectK.BusinessLogic.Modules.KurinModule.Services;
using ProjectK.Common.Entities.KurinModule.Agenda;
using ProjectK.Common.Enums.KurinModule;

namespace ProjectK.BusinessLogic.Tests.KurinModule.HandlerTests.AgendaHandlers;

public class AgendaRecurrenceRulesTests
{
    private static AgendaItem Item(DateTime start, DateTime? end = null) => new()
    {
        KurinKey = Guid.NewGuid(),
        StartUtc = start,
        EndUtc = end
    };

    private static DateTime Utc(int y, int m, int d, int h = 9) => new(y, m, d, h, 0, 0, DateTimeKind.Utc);

    /// <summary>
    /// Found live: сходини every Friday at 18:30 Kyiv showed as 17:30 from the Friday after the
    /// clocks went back, because the UTC start was stepped by whole days.
    /// </summary>
    [Fact]
    public void Weekly_KeepsTheWallClockTime_AcrossTheAutumnClockChange()
    {
        var kyiv = AgendaRecurrenceRules.ResolveZone("Europe/Kyiv");
        // Friday 9 Oct 2026, 18:30 Kyiv = 15:30 UTC (summer time).
        var item = Item(new DateTime(2026, 10, 9, 15, 30, 0, DateTimeKind.Utc));
        item.RecurrenceFrequency = RecurrenceFrequency.Weekly;
        item.RecurrenceInterval = 1;

        var occ = AgendaRecurrenceRules.Expand(item, Utc(2026, 10, 1), Utc(2026, 11, 10), kyiv).ToList();

        occ.Select(o => o.StartUtc).Should().Equal(
            new DateTime(2026, 10, 9, 15, 30, 0, DateTimeKind.Utc),
            new DateTime(2026, 10, 16, 15, 30, 0, DateTimeKind.Utc),
            new DateTime(2026, 10, 23, 15, 30, 0, DateTimeKind.Utc),
            // Clocks went back on 25 Oct: 18:30 Kyiv is now 16:30 UTC.
            new DateTime(2026, 10, 30, 16, 30, 0, DateTimeKind.Utc),
            new DateTime(2026, 11, 6, 16, 30, 0, DateTimeKind.Utc));
        occ.Should().OnlyContain(o => TimeZoneInfo.ConvertTimeFromUtc(o.StartUtc, kyiv).TimeOfDay == new TimeSpan(18, 30, 0));
    }

    /// <summary>A series on the hour the spring change skips lands an hour later, not nowhere.</summary>
    [Fact]
    public void Weekly_OnTheSkippedHour_LandsAnHourLater()
    {
        var kyiv = AgendaRecurrenceRules.ResolveZone("Europe/Kyiv");
        // Sunday 22 Mar 2026, 03:30 Kyiv = 01:30 UTC; on 29 Mar the clock jumps from 03:00 to 04:00.
        var item = Item(new DateTime(2026, 3, 22, 1, 30, 0, DateTimeKind.Utc));
        item.RecurrenceFrequency = RecurrenceFrequency.Weekly;
        item.RecurrenceInterval = 1;

        var occ = AgendaRecurrenceRules.Expand(item, Utc(2026, 3, 20), Utc(2026, 4, 2), kyiv).ToList();

        occ.Select(o => o.StartUtc).Should().Equal(
            new DateTime(2026, 3, 22, 1, 30, 0, DateTimeKind.Utc),
            new DateTime(2026, 3, 29, 1, 30, 0, DateTimeKind.Utc));
    }

    [Fact]
    public void NoRecurrence_YieldsSingleOccurrence()
    {
        var item = Item(Utc(2026, 8, 3));
        item.RecurrenceFrequency = RecurrenceFrequency.None;

        var occ = AgendaRecurrenceRules.Expand(item, Utc(2026, 8, 1), Utc(2026, 8, 31), TimeZoneInfo.Utc).ToList();

        occ.Should().ContainSingle().Which.StartUtc.Should().Be(Utc(2026, 8, 3));
    }

    [Fact]
    public void Weekly_EveryWeek_YieldsOnePerWeekInWindow()
    {
        // Monday 3 Aug 2026, weekly, default weekday (its own).
        var item = Item(Utc(2026, 8, 3));
        item.RecurrenceFrequency = RecurrenceFrequency.Weekly;
        item.RecurrenceInterval = 1;

        var occ = AgendaRecurrenceRules.Expand(item, Utc(2026, 8, 1), Utc(2026, 8, 31), TimeZoneInfo.Utc).ToList();

        occ.Select(o => o.StartUtc).Should().Equal(
            Utc(2026, 8, 3), Utc(2026, 8, 10), Utc(2026, 8, 17), Utc(2026, 8, 24), Utc(2026, 8, 31));
    }

    [Fact]
    public void Weekly_WithWeekdayMask_YieldsSelectedDays()
    {
        // Start Mon 3 Aug; mask = Tue+Thu (bit2 + bit4).
        var item = Item(Utc(2026, 8, 3));
        item.RecurrenceFrequency = RecurrenceFrequency.Weekly;
        item.RecurrenceByWeekday = (1 << 2) | (1 << 4); // Tuesday, Thursday

        var occ = AgendaRecurrenceRules.Expand(item, Utc(2026, 8, 3), Utc(2026, 8, 14), TimeZoneInfo.Utc).ToList();

        occ.Select(o => o.StartUtc).Should().Equal(
            Utc(2026, 8, 4), Utc(2026, 8, 6), Utc(2026, 8, 11), Utc(2026, 8, 13));
    }

    [Fact]
    public void Weekly_RespectsCountAcrossWholeSeries()
    {
        var item = Item(Utc(2026, 8, 3));
        item.RecurrenceFrequency = RecurrenceFrequency.Weekly;
        item.RecurrenceCount = 3;

        var occ = AgendaRecurrenceRules.Expand(item, Utc(2026, 8, 1), Utc(2026, 12, 31), TimeZoneInfo.Utc).ToList();

        occ.Select(o => o.StartUtc).Should().Equal(Utc(2026, 8, 3), Utc(2026, 8, 10), Utc(2026, 8, 17));
    }

    [Fact]
    public void Weekly_RespectsEndDate()
    {
        var item = Item(Utc(2026, 8, 3));
        item.RecurrenceFrequency = RecurrenceFrequency.Weekly;
        item.RecurrenceEndUtc = Utc(2026, 8, 17, 23);

        var occ = AgendaRecurrenceRules.Expand(item, Utc(2026, 8, 1), Utc(2026, 9, 30), TimeZoneInfo.Utc).ToList();

        occ.Should().HaveCount(3);
        occ.Last().StartUtc.Should().Be(Utc(2026, 8, 17));
    }

    [Fact]
    public void Monthly_KeepsDayOfMonth_AndCarriesDuration()
    {
        var item = Item(Utc(2026, 1, 15, 18), Utc(2026, 1, 15, 20));
        item.RecurrenceFrequency = RecurrenceFrequency.Monthly;

        var occ = AgendaRecurrenceRules.Expand(item, Utc(2026, 1, 1), Utc(2026, 4, 30), TimeZoneInfo.Utc).ToList();

        occ.Select(o => o.StartUtc).Should().Equal(
            Utc(2026, 1, 15, 18), Utc(2026, 2, 15, 18), Utc(2026, 3, 15, 18), Utc(2026, 4, 15, 18));
        occ.First().EndUtc.Should().Be(Utc(2026, 1, 15, 20));
    }

    [Fact]
    public void Monthly_SkipsMonthsWithoutThatDay()
    {
        // 31st: Feb/Apr/Jun... have no 31st and are skipped, not shifted.
        var item = Item(Utc(2026, 1, 31, 9));
        item.RecurrenceFrequency = RecurrenceFrequency.Monthly;

        var occ = AgendaRecurrenceRules.Expand(item, Utc(2026, 1, 1), Utc(2026, 5, 31), TimeZoneInfo.Utc).ToList();

        occ.Select(o => o.StartUtc).Should().Equal(Utc(2026, 1, 31, 9), Utc(2026, 3, 31, 9), Utc(2026, 5, 31, 9));
    }

    [Fact]
    public void Yearly_KeepsMonthAndDay()
    {
        var item = Item(Utc(2024, 6, 12));
        item.RecurrenceFrequency = RecurrenceFrequency.Yearly;

        var occ = AgendaRecurrenceRules.Expand(item, Utc(2026, 1, 1), Utc(2027, 12, 31), TimeZoneInfo.Utc).ToList();

        occ.Select(o => o.StartUtc).Should().Equal(Utc(2026, 6, 12), Utc(2027, 6, 12));
    }
}
