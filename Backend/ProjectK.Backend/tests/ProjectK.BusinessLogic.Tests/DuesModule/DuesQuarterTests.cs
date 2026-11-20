using FluentAssertions;
using ProjectK.Common.Records.DuesModule;
using ProjectK.Common.Rules.DuesModule;

namespace ProjectK.BusinessLogic.Tests.DuesModule;

public class DuesQuarterTests
{
    [Theory]
    [InlineData(1, 1)]
    [InlineData(3, 1)]
    [InlineData(4, 2)]
    [InlineData(9, 3)]
    [InlineData(10, 4)]
    [InlineData(12, 4)]
    public void Of_PutsAMonthInItsCalendarQuarter(int month, int quarter)
    {
        DuesQuarter.Of(new DateOnly(2025, month, 15)).Should().Be(new DuesQuarter(2025, quarter));
    }

    [Fact]
    public void Index_RoundTripsAndOrders()
    {
        var q = new DuesQuarter(2025, 4);

        DuesQuarter.FromIndex(q.Index).Should().Be(q);
        q.Next().Should().Be(new DuesQuarter(2026, 1));
        (q < q.Next()).Should().BeTrue();
    }

    [Fact]
    public void PlastYear_TheThirdQuarterStaysWithTheYearThatEnds()
    {
        PlastYearRules.Of(new DuesQuarter(2025, 3)).Should().Be(2024);
        PlastYearRules.Of(new DuesQuarter(2025, 4)).Should().Be(2025);
        PlastYearRules.Of(new DuesQuarter(2026, 3)).Should().Be(2025);
    }

    [Fact]
    public void PlastYear_25_26_IsTheFourthOf2025AndTheFirstThreeOf2026()
    {
        PlastYearRules.QuartersOf(2025).Should().Equal(
            new DuesQuarter(2025, 4), new DuesQuarter(2026, 1), new DuesQuarter(2026, 2), new DuesQuarter(2026, 3));
        PlastYearRules.Label(2025).Should().Be("25–26");
    }
}
