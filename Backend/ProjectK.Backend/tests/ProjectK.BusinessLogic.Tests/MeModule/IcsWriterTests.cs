using System.Text;
using FluentAssertions;
using ProjectK.BusinessLogic.Modules.MeModule.Services;
using Xunit;

namespace ProjectK.BusinessLogic.Tests.MeModule;

/// <summary>The few rules of RFC 5545 a calendar app is strict about.</summary>
public class IcsWriterTests
{
    private static readonly DateTime Stamp = new(2026, 10, 20, 8, 0, 0, DateTimeKind.Utc);

    private static string One(IcsEvent item) => IcsWriter.Write("Лілейка", "Europe/Kyiv", [item]);

    private static IcsEvent Timed(string summary = "Сходини", string? description = null) => new(
        "a@lileyka.app", new DateTime(2026, 10, 27, 16, 30, 0, DateTimeKind.Utc), new DateTime(2026, 10, 27, 18, 0, 0, DateTimeKind.Utc),
        false, summary, description, "Домівка", "Сходини", Stamp);

    /// <summary>Folded lines joined back, as a reader does: CRLF followed by one space is no break.</summary>
    private static string[] Unfolded(string ics) => ics.Replace("\r\n ", string.Empty, StringComparison.Ordinal).Split("\r\n");

    [Fact]
    public void EveryLine_EndsInCrLf()
    {
        var ics = One(Timed());

        ics.Should().EndWith("END:VCALENDAR\r\n");
        ics.Replace("\r\n", string.Empty, StringComparison.Ordinal).Should().NotContain("\n");
    }

    [Fact]
    public void ATimedEvent_IsWrittenInUtc()
    {
        var lines = Unfolded(One(Timed()));

        lines.Should().Contain("DTSTART:20261027T163000Z");
        lines.Should().Contain("DTEND:20261027T180000Z");
    }

    [Fact]
    public void AnAllDayEvent_EndsTheDayAfterItsLastDay()
    {
        var day = new DateTime(2026, 11, 7, 0, 0, 0, DateTimeKind.Utc);
        var lines = Unfolded(One(new IcsEvent("b@lileyka.app", day, day.AddDays(1), true, "Табір", null, null, null, Stamp)));

        lines.Should().Contain("DTSTART;VALUE=DATE:20261107");
        lines.Should().Contain("DTEND;VALUE=DATE:20261109");
    }

    [Fact]
    public void Text_EscapesItsSeparatorsAndNewlines()
    {
        var lines = Unfolded(One(Timed("Сходини; гра, вогнище\\", "Перший рядок\r\nдругий")));

        lines.Should().Contain(@"SUMMARY:Сходини\; гра\, вогнище\\");
        lines.Should().Contain(@"DESCRIPTION:Перший рядок\nдругий");
    }

    [Fact]
    public void ALongCyrillicLine_IsFoldedAt75Octets_WithoutSplittingALetter()
    {
        var title = string.Concat(Enumerable.Repeat("Сходини гуртка Кельти ", 8));
        var ics = One(Timed(title));

        foreach (var line in ics.Split("\r\n"))
        {
            Encoding.UTF8.GetByteCount(line).Should().BeLessThanOrEqualTo(75);
            line.Should().NotContain("�");
        }
        Unfolded(ics).Should().Contain($"SUMMARY:{title}");
    }
}
