using FluentAssertions;
using ProjectK.Common.Rules.Shared;

namespace ProjectK.BusinessLogic.Tests.CommonTests;

public class JsonListRulesTests
{
    [Fact]
    public void ReadStrings_ReadsTheArrayInOrder()
    {
        JsonListRules.ReadStrings("[\"profile\",\"skills\"]").Should().Equal("profile", "skills");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not json")]
    [InlineData("{\"a\":1}")]
    [InlineData("[1,2]")]
    [InlineData("null")]
    public void ReadStrings_FallsBackToEmpty_ForABlankOrDamagedColumn(string? json)
    {
        JsonListRules.ReadStrings(json).Should().BeEmpty();
    }
}
