using ProjectK.Common.Rules.Shared;

namespace ProjectK.BusinessLogic.Tests.CommonTests;

/// <summary>
/// A release may be published without a code name. Everything that prints one has to drop the
/// quotes with it, rather than show the version followed by an empty pair.
/// </summary>
public class ReleaseDisplayRulesTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void NoCodeName_ReadsAsAbsent(string? configured)
    {
        Assert.Null(ReleaseDisplayRules.CodeName(configured));
    }

    [Fact]
    public void CodeName_IsTrimmed()
    {
        Assert.Equal("Honeypot Ant", ReleaseDisplayRules.CodeName("  Honeypot Ant  "));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void Label_WithoutCodeName_IsTheVersionAlone(string? codeName)
    {
        Assert.Equal("1.0", ReleaseDisplayRules.Label("1.0", codeName));
    }

    [Fact]
    public void Label_WithCodeName_QuotesIt()
    {
        Assert.Equal("0.20.0-beta \"Honeypot Ant\"", ReleaseDisplayRules.Label("0.20.0-beta", "Honeypot Ant"));
    }

    [Fact]
    public void Label_WithoutVersion_SaysUnknownRatherThanNothing()
    {
        Assert.Equal("unknown", ReleaseDisplayRules.Label(null, null));
    }

    /// <summary>A two-part version is a version like any other; nothing here reads it as semver.</summary>
    [Fact]
    public void Label_AcceptsATwoPartVersion()
    {
        Assert.Equal("1.0", ReleaseDisplayRules.Label("v1.0".TrimStart('v'), null));
    }
}
