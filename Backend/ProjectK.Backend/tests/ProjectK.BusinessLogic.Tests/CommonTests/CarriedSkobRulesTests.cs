using FluentAssertions;
using ProjectK.Common.Enums.Shared;
using ProjectK.Common.Rules.Shared;

namespace ProjectK.BusinessLogic.Tests.CommonTests;

/// <summary>A старший пластун who earned a скоб in УПЮ is «ст. пл. скоб»; a сеніор is just a сеніор.</summary>
public class CarriedSkobRulesTests
{
    [Fact]
    public void TheHetmanskiySkob_LeadsThePlainOne_AndNoSkobIsNone()
    {
        PlastLevelDegreeRules.CarriedSkob([PlastLevel.Skob, PlastLevel.HetmanskiySkob, PlastLevel.Starshoplastun])
            .Should().Be(PlastLevel.HetmanskiySkob);
        PlastLevelDegreeRules.CarriedSkob([PlastLevel.Skob, PlastLevel.Starshoplastun]).Should().Be(PlastLevel.Skob);
        PlastLevelDegreeRules.CarriedSkob([PlastLevel.Rozviduvach, PlastLevel.Starshoplastun]).Should().BeNull();
    }

    [Theory]
    [InlineData(PlastLevel.Skob, false, "скоб")]
    [InlineData(PlastLevel.Skob, true, "вірлиця")]
    [InlineData(PlastLevel.HetmanskiySkob, false, "гетьманський скоб")]
    [InlineData(PlastLevel.HetmanskiySkob, true, "гетьманська вірлиця")]
    public void TheCarriedSkob_ReadsForAPlastunOrAPlastunka(PlastLevel skob, bool isPlastunka, string name) =>
        PlastLevelDegreeRules.CarriedSkobName(skob, isPlastunka).Should().Be(name);

    [Fact]
    public void TheShortName_JoinsTheSkob_OnlyToAStarshyiPlastun()
    {
        PlastLevelNames.Of(PlastLevel.Starshoplastun, PlastLevel.Skob).Should().Be("ст. пл. скоб");
        PlastLevelNames.Of(PlastLevel.Starshoplastun, PlastLevel.HetmanskiySkob).Should().Be("ст. пл. гетьм. скоб");
        PlastLevelNames.Of(PlastLevel.Starshoplastun, null).Should().Be("ст. пл.");
        PlastLevelNames.Of(PlastLevel.Senior, PlastLevel.Skob).Should().Be("пл. сен.");
    }
}
