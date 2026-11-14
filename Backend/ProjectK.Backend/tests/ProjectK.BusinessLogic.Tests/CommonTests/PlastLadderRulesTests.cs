using FluentAssertions;
using ProjectK.Common.Enums.KurinModule;
using ProjectK.Common.Enums.Shared;
using ProjectK.Common.Rules.Shared;

namespace ProjectK.BusinessLogic.Tests.CommonTests;

/// <summary>The branch a person belongs to is read off their ступінь first and their kurin second.</summary>
public class PlastLadderRulesTests
{
    [Theory]
    [InlineData(PlastLevel.Entry, KurinBranch.UPYu)]
    [InlineData(PlastLevel.Prykhylnyk, KurinBranch.UPYu)]
    [InlineData(PlastLevel.HetmanskiySkob, KurinBranch.UPYu)]
    [InlineData(PlastLevel.Starshoplastun, KurinBranch.USP)]
    [InlineData(PlastLevel.Senior, KurinBranch.UPS)]
    [InlineData(PlastLevel.SeniorKerivnytstva, KurinBranch.UPS)]
    public void EveryStupin_BelongsToOneBranch(PlastLevel level, KurinBranch branch) =>
        PlastLadderRules.BranchOf(level).Should().Be(branch);

    // Prykhylnyk is 10 by number and second on the ladder: the ladder wins.
    [Fact]
    public void Highest_GoesByTheLadder_NotTheNumber()
    {
        PlastLadderRules.Highest([PlastLevel.Prykhylnyk, PlastLevel.Uchasnyk]).Should().Be(PlastLevel.Uchasnyk);
        PlastLadderRules.Highest([PlastLevel.Entry, PlastLevel.Senior, PlastLevel.Skob]).Should().Be(PlastLevel.Senior);
        PlastLadderRules.Highest([]).Should().BeNull();
    }

    [Fact]
    public void AVporiadnykWithASeniorStupin_IsNotAYouth_EvenInAYouthKurin() =>
        PlastLadderRules.PersonalBranch([PlastLevel.Skob, PlastLevel.Starshoplastun], KurinBranch.UPYu).Should().Be(KurinBranch.USP);

    [Fact]
    public void WithoutASeniorStupin_TheKurinDecides()
    {
        PlastLadderRules.PersonalBranch([PlastLevel.Skob], KurinBranch.UPYu).Should().Be(KurinBranch.UPYu);
        PlastLadderRules.PersonalBranch([], KurinBranch.USP).Should().Be(KurinBranch.USP);
        PlastLadderRules.PersonalBranch([], null).Should().Be(KurinBranch.UPYu);
    }
}
