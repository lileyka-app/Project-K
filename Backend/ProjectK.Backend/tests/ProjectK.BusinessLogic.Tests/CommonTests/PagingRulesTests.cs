using FluentAssertions;
using ProjectK.Common.Rules.Shared;

namespace ProjectK.BusinessLogic.Tests.CommonTests;

public class PagingRulesTests
{
    [Theory]
    [InlineData(-5, 0)]
    [InlineData(0, 0)]
    [InlineData(40, 40)]
    public void Skip_StartsAtTheTop_ForANegativeRequest(int skip, int expected)
    {
        PagingRules.Skip(skip).Should().Be(expected);
    }

    [Theory]
    [InlineData(-1, 1)]
    [InlineData(0, 1)]
    [InlineData(20, 20)]
    [InlineData(50, 50)]
    [InlineData(51, 50)]
    [InlineData(int.MaxValue, 50)]
    public void Take_HoldsThePageBetweenOneAndTheCeiling(int take, int expected)
    {
        PagingRules.Take(take, 50).Should().Be(expected);
    }
}
