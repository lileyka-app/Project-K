using FluentAssertions;
using ProjectK.BusinessLogic.Modules.MemberModule.Services;
using ProjectK.Common.Entities.MemberModule;
using ProjectK.Common.Enums.KurinModule;
using ProjectK.Common.Enums.Shared;
using ProjectK.Common.Rules.MemberModule;
using ProjectK.Common.Rules.Shared;

namespace ProjectK.BusinessLogic.Tests.CommonTests;

/// <summary>The two things the server says about a person for the client to render by: the улад and adulthood.</summary>
public class PersonUladAndAgeRulesTests
{
    private static readonly DateOnly Today = new(2026, 11, 17);

    [Theory]
    [InlineData(2008, 11, 17, true)]
    [InlineData(2008, 11, 18, false)]
    [InlineData(1990, 1, 1, true)]
    [InlineData(2015, 6, 1, false)]
    public void IsAdult_CountsEighteenYearsToTheDay(int year, int month, int day, bool adult)
    {
        AdulthoodRules.IsAdult(new DateOnly(year, month, day), Today).Should().Be(adult);
    }

    [Fact]
    public void IsAdult_WithoutADate_IsAMinor()
    {
        AdulthoodRules.IsAdult(null, Today).Should().BeFalse();
        AdulthoodRules.IsAdult(default(DateOnly), Today).Should().BeFalse();
    }

    [Fact]
    public void Ulad_ASeniorLevelOutweighsTheKurin_ElseTheKurinDecides()
    {
        UladRules.Of([PlastLevel.Skob, PlastLevel.Starshoplastun], KurinBranch.UPYu).Should().Be(KurinBranch.USP);
        UladRules.Of([PlastLevel.Skob], KurinBranch.UPS).Should().Be(KurinBranch.UPS);
        UladRules.Of([], (KurinBranch?)null).Should().Be(KurinBranch.UPYu);
    }

    [Fact]
    public void Ulad_AcrossKurins_KeepsTheYouthProgrammeWhileAnUpyuKurinHoldsThem()
    {
        UladRules.Of([PlastLevel.Uchasnyk], [KurinBranch.USP, KurinBranch.UPYu]).Should().Be(KurinBranch.UPYu);
        UladRules.Of([], [KurinBranch.UPS]).Should().Be(KurinBranch.UPS);
        UladRules.Of([PlastLevel.Senior], [KurinBranch.UPYu]).Should().Be(KurinBranch.UPS);
    }

    [Fact]
    public void CountingLevels_LeaveOutAWaitingLevel_SoItDoesNotMoveTheUlad()
    {
        var history = new List<PlastLevelHistory>
        {
            new() { PlastLevel = PlastLevel.Skob },
            new() { PlastLevel = PlastLevel.Starshoplastun, AwaitsConfirmation = true }
        };

        var counting = PlastLevelHistoryRules.CountingLevels(history, null);

        counting.Should().Equal(PlastLevel.Skob);
        UladRules.Of(counting, KurinBranch.UPYu).Should().Be(KurinBranch.UPYu);
    }

    [Fact]
    public void CountingLevels_KeepTheStoredLatestOfAnOlderRecord()
    {
        PlastLevelHistoryRules.CountingLevels([], PlastLevel.Senior).Should().Equal(PlastLevel.Senior);
    }
}
