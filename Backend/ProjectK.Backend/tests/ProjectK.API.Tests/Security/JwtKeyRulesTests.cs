using ProjectK.API.Hosting;

namespace ProjectK.API.Tests.Security;

/// <summary>
/// SEC-4.5: the API refuses to start on a key that would let anyone who has read the example file
/// sign a session.
/// </summary>
public class JwtKeyRulesTests
{
    private const string RealKey = "k3Zp9vQ2mX7nB4cL8wR1tY6uH0sD5fG3jA2eN8qV";

    [Theory]
    [InlineData("Production")]
    [InlineData("SelfHost")]
    [InlineData("Development")]
    public void Refusal_ShouldAcceptARealKey_OnEveryTier(string tier)
    {
        Assert.Null(JwtKeyRules.Refusal(RealKey, tier));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Refusal_ShouldRefuseAMissingKey_EvenLocally(string? key)
    {
        Assert.NotNull(JwtKeyRules.Refusal(key, "Development"));
    }

    [Fact]
    public void Refusal_ShouldRefuseAShortKey_EvenLocally()
    {
        Assert.NotNull(JwtKeyRules.Refusal("too-short-for-hmac-sha256", "Development"));
    }

    [Theory]
    [InlineData("replace-this-with-at-least-32-random-characters", "SelfHost")]
    [InlineData("set-a-real-staging-jwt-key-at-least-32-characters", "Staging")]
    [InlineData("local-selfhost-jwt-key-change-me-please-at-least-32", "Production")]
    public void Refusal_ShouldRefuseTheTemplateWording_OnADeployedTier(string key, string tier)
    {
        Assert.NotNull(JwtKeyRules.Refusal(key, tier));
    }

    /// <summary>The committed local keys are the point of the local tiers; they are not refused there.</summary>
    [Theory]
    [InlineData("Development")]
    [InlineData("E2E")]
    public void Refusal_ShouldLetTheTemplateWordingThrough_OnALocalTier(string tier)
    {
        Assert.Null(JwtKeyRules.Refusal("local-development-jwt-key-change-me-please-32+", tier));
    }
}
