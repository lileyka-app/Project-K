using FluentAssertions;
using ProjectK.Common.Rules.Shared;

namespace ProjectK.BusinessLogic.Tests.CommonTests;

public class TextRulesTests
{
    [Theory]
    [InlineData("Стрийська 5", "Стрийська 5")]
    [InlineData("  Стрийська 5 \t", "Стрийська 5")]
    [InlineData("a  b", "a  b")]
    [InlineData("", null)]
    [InlineData("   ", null)]
    [InlineData("  \n", null)]
    [InlineData(null, null)]
    public void TrimOrNull_TrimsTheEnds_AndKeepsBlankAsNothing(string? value, string? expected)
    {
        TextRules.TrimOrNull(value).Should().Be(expected);
    }
}
