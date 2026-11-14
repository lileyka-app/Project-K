using ProjectK.Common.Enums.KurinModule;
using ProjectK.Common.Enums.Shared;
using ProjectK.Common.Rules.KurinModule;
using ProjectK.Common.Rules.Shared;

namespace ProjectK.API.Tests.Services.Reports;

public sealed class KurinReportNamesTests
{
    [Theory]
    [InlineData(MemberWarningLevel.Level1, "1-а пересторога")]
    [InlineData(MemberWarningLevel.Level2, "2-а пересторога")]
    [InlineData(MemberWarningLevel.Level3, "3-а пересторога")]
    public void WarningLevel_ShouldUseHumanReadableLabels(MemberWarningLevel level, string expected)
    {
        Assert.Equal(expected, KurinReportNames.WarningLevel(level));
    }

    [Theory]
    [InlineData(MemberAwardLevel.First, "1-е відзначення")]
    [InlineData(MemberAwardLevel.Second, "2-е відзначення")]
    [InlineData(MemberAwardLevel.Third, "3-е відзначення")]
    [InlineData(MemberAwardLevel.Fourth, "4-е відзначення")]
    public void AwardLevel_ShouldUseHumanReadableLabels(MemberAwardLevel level, string expected)
    {
        Assert.Equal(expected, KurinReportNames.AwardLevel(level));
    }

    /// <summary>
    /// The звіт says what the реєстр says. It used to have wording of its own — «Скоб» here,
    /// «пл. скоб / вірл.» on screen — so both are pinned to <see cref="PlastLevelNames"/> and a
    /// change to one is a change to both.
    /// </summary>
    [Theory]
    [InlineData(PlastLevel.Entry)]
    [InlineData(PlastLevel.Uchasnyk)]
    [InlineData(PlastLevel.Skob)]
    [InlineData(PlastLevel.SeniorKerivnytstva)]
    public void PlastLevel_ShouldSayWhatTheRegistrySays(PlastLevel level)
    {
        Assert.Equal(PlastLevelNames.Of(level), KurinReportNames.PlastLevel(level));
    }

    [Fact]
    public void PlastLevel_ShouldUseHumanReadableLabels()
    {
        Assert.Equal("пл. скоб / вірл.", KurinReportNames.PlastLevel(PlastLevel.Skob));
        Assert.Equal("-", KurinReportNames.PlastLevel(null));
    }
}
