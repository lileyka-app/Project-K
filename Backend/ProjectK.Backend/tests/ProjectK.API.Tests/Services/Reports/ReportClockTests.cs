using ProjectK.Infrastructure.Services.Reports;

namespace ProjectK.API.Tests.Services.Reports;

/// <summary>
/// The звіт prints the reader's hours, not the server's. The zone arrives from the browser, so half
/// of what is asserted here is about surviving what a client might send.
/// </summary>
public sealed class ReportClockTests
{
    private static readonly DateTime WinterNoonUtc = new(2026, 1, 15, 12, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime SummerNoonUtc = new(2026, 7, 15, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Format_ShouldPrintTheHoursOfTheGivenZone()
    {
        var kyiv = ReportClock.Resolve("Europe/Kyiv");

        Assert.Equal("2026-01-15 14:00", ReportClock.Format(WinterNoonUtc, kyiv));
    }

    /// <summary>Kyiv is two hours ahead in winter and three in summer, and each timestamp is its own moment.</summary>
    [Fact]
    public void Format_ShouldFollowDaylightSaving()
    {
        var kyiv = ReportClock.Resolve("Europe/Kyiv");

        Assert.Equal("2026-07-15 15:00", ReportClock.Format(SummerNoonUtc, kyiv));
    }

    /// <summary>
    /// EF hands <c>datetime2</c> back with no kind at all. Treating that as local would shift it by
    /// the server's own offset — nothing on a UTC container, three hours on a developer's machine.
    /// </summary>
    [Fact]
    public void Format_ShouldReadAnUnspecifiedTimestampAsUtc()
    {
        var kyiv = ReportClock.Resolve("Europe/Kyiv");
        var fromDatabase = DateTime.SpecifyKind(WinterNoonUtc, DateTimeKind.Unspecified);

        Assert.Equal(ReportClock.Format(WinterNoonUtc, kyiv), ReportClock.Format(fromDatabase, kyiv));
    }

    [Fact]
    public void Format_ShouldSayNothingHappenedRatherThanPrintAZeroDate()
    {
        Assert.Equal("-", ReportClock.Format(null, TimeZoneInfo.Utc));
    }

    /// <summary>
    /// The id comes off the wire. A typo, a zone this machine has never heard of, or junk must leave
    /// the report standing — printing UTC is a small loss, failing the export is not.
    /// </summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Not/AZone")]
    [InlineData("../../etc/passwd")]
    public void Resolve_ShouldFallBackToUtc_WhenTheZoneIsNotOne(string? id)
    {
        var zone = ReportClock.Resolve(id);

        Assert.Equal(TimeZoneInfo.Utc, zone);
        Assert.Equal("UTC", ReportClock.Label(zone));
        Assert.Equal("2026-01-15 12:00", ReportClock.Format(WinterNoonUtc, zone));
    }

    [Fact]
    public void Label_ShouldNameTheZoneSoTheHoursAreNotAmbiguous()
    {
        Assert.Equal("Europe/Kyiv", ReportClock.Label(ReportClock.Resolve("Europe/Kyiv")));
    }
}
