using FluentAssertions;
using ProjectK.Common.Models.Enums;

namespace ProjectK.BusinessLogic.Tests.CommonTests;

/// <summary>The branch a person belongs to is read off their ступінь first and their kurin second.</summary>
public class PlastLadderTests
{
    [Theory]
    [InlineData(PlastLevel.Entry, KurinBranch.UPYu)]
    [InlineData(PlastLevel.Prykhylnyk, KurinBranch.UPYu)]
    [InlineData(PlastLevel.HetmanskiySkob, KurinBranch.UPYu)]
    [InlineData(PlastLevel.Starshoplastun, KurinBranch.USP)]
    [InlineData(PlastLevel.Senior, KurinBranch.UPS)]
    [InlineData(PlastLevel.SeniorKerivnytstva, KurinBranch.UPS)]
    public void EveryStupin_BelongsToOneBranch(PlastLevel level, KurinBranch branch) =>
        PlastLadder.BranchOf(level).Should().Be(branch);

    // Prykhylnyk is 10 by number and second on the ladder: the ladder wins.
    [Fact]
    public void Highest_GoesByTheLadder_NotTheNumber()
    {
        PlastLadder.Highest([PlastLevel.Prykhylnyk, PlastLevel.Uchasnyk]).Should().Be(PlastLevel.Uchasnyk);
        PlastLadder.Highest([PlastLevel.Entry, PlastLevel.Senior, PlastLevel.Skob]).Should().Be(PlastLevel.Senior);
        PlastLadder.Highest([]).Should().BeNull();
    }

    [Fact]
    public void AVporiadnykWithASeniorStupin_IsNotAYouth_EvenInAYouthKurin() =>
        PlastLadder.PersonalBranch([PlastLevel.Skob, PlastLevel.Starshoplastun], KurinBranch.UPYu).Should().Be(KurinBranch.USP);

    [Fact]
    public void WithoutASeniorStupin_TheKurinDecides()
    {
        PlastLadder.PersonalBranch([PlastLevel.Skob], KurinBranch.UPYu).Should().Be(KurinBranch.UPYu);
        PlastLadder.PersonalBranch([], KurinBranch.USP).Should().Be(KurinBranch.USP);
        PlastLadder.PersonalBranch([], null).Should().Be(KurinBranch.UPYu);
    }
}
