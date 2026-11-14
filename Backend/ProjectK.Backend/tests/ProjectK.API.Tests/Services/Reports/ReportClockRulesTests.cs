using ProjectK.Infrastructure.Services.Reports;

namespace ProjectK.API.Tests.Services.Reports;

/// <summary>
/// The звіт prints the reader's hours, not the server's. The zone arrives from the browser, so half
/// of what is asserted here is about surviving what a client might send.
/// </summary>
public sealed class ReportClockRulesTests
{
    private static readonly DateTime WinterNoonUtc = new(2026, 1, 15, 12, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime SummerNoonUtc = new(2026, 7, 15, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Format_ShouldPrintTheHoursOfTheGivenZone()
    {
        var kyiv = ReportClockRules.Resolve("Europe/Kyiv");

        Assert.Equal("2026-01-15 14:00", ReportClockRules.Format(WinterNoonUtc, kyiv));
    }

    /// <summary>Kyiv is two hours ahead in winter and three in summer, and each timestamp is its own moment.</summary>
    [Fact]
    public void Format_ShouldFollowDaylightSaving()
    {
        var kyiv = ReportClockRules.Resolve("Europe/Kyiv");

        Assert.Equal("2026-07-15 15:00", ReportClockRules.Format(SummerNoonUtc, kyiv));
    }

    /// <summary>
    /// EF hands <c>datetime2</c> back with no kind at all. Treating that as local would shift it by
    /// the server's own offset — nothing on a UTC container, three hours on a developer's machine.
    /// </summary>
    [Fact]
    public void Format_ShouldReadAnUnspecifiedTimestampAsUtc()
    {
        var kyiv = ReportClockRules.Resolve("Europe/Kyiv");
        var fromDatabase = DateTime.SpecifyKind(WinterNoonUtc, DateTimeKind.Unspecified);

        Assert.Equal(ReportClockRules.Format(WinterNoonUtc, kyiv), ReportClockRules.Format(fromDatabase, kyiv));
    }

    [Fact]
    public void Format_ShouldSayNothingHappenedRatherThanPrintAZeroDate()
    {
        Assert.Equal("-", ReportClockRules.Format(null, TimeZoneInfo.Utc));
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
        var zone = ReportClockRules.Resolve(id);

        Assert.Equal(TimeZoneInfo.Utc, zone);
        Assert.Equal("UTC", ReportClockRules.Label(zone));
        Assert.Equal("2026-01-15 12:00", ReportClockRules.Format(WinterNoonUtc, zone));
    }

    [Fact]
    public void Label_ShouldNameTheZoneSoTheHoursAreNotAmbiguous()
    {
        Assert.Equal("Europe/Kyiv", ReportClockRules.Label(ReportClockRules.Resolve("Europe/Kyiv")));
    }
}
