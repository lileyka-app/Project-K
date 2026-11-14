using FluentAssertions;
using ProjectK.Common.Entities.KraiModule;

namespace ProjectK.BusinessLogic.Tests.CommonTests;

/// <summary>
/// The krai module reads a code as <c>NormalizeCode(code) ?? ""</c>; that has to give what the inline
/// <c>code?.Trim().ToLowerInvariant() ?? ""</c> it replaced gave, blank codes included.
/// </summary>
public class KraiCodeTests
{
    [Theory]
    [InlineData("pl", "pl")]
    [InlineData(" PL ", "pl")]
    [InlineData("Sk", "sk")]
    [InlineData("", "")]
    [InlineData("   ", "")]
    [InlineData(null, "")]
    public void NormalizeCode_OrEmpty_ReadsACodeAsTheInlineFormDid(string? code, string expected)
    {
        (Krai.NormalizeCode(code) ?? string.Empty).Should().Be(expected);
        (code?.Trim().ToLowerInvariant() ?? string.Empty).Should().Be(expected);
    }

    [Fact]
    public void NormalizeCode_ReadsABlankCodeAsNone()
    {
        Krai.NormalizeCode("  ").Should().BeNull();
    }
}
