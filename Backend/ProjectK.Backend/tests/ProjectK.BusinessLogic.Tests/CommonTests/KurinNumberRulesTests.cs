using FluentAssertions;
using ProjectK.Common.Options.KurinModule;
using ProjectK.Common.Rules.KurinModule;

namespace ProjectK.BusinessLogic.Tests.CommonTests;

/// <summary>The kurin number's one range message and its one file-name form.</summary>
public class KurinNumberRulesTests
{
    [Fact]
    public void OutOfRangeMessage_NamesTheFixedFloorAndTheConfiguredCeiling()
    {
        new KurinNumberOptions { MaxNumber = 150 }.OutOfRangeMessage
            .Should().Be("Kurin number must be between 1 and 150.");
    }

    [Fact]
    public void OutOfRangeMessage_UsesTheDefaultCeiling_WhenNothingIsConfigured()
    {
        new KurinNumberOptions().OutOfRangeMessage
            .Should().Be("Kurin number must be between 1 and 200.");
    }

    [Theory]
    [InlineData(47, "47")]
    [InlineData(0, "0")]
    [InlineData(null, "pidhotovchyi")]
    public void InFileName_FallsBackToPidhotovchyi_WithoutANumber(int? number, string expected)
    {
        KurinBranchNames.InFileName(number).Should().Be(expected);
    }
}
