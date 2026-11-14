using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Moq;
using ProjectK.BusinessLogic.Modules.AuthModule.Services;
using ProjectK.Common.Rules.Shared;

namespace ProjectK.API.Tests.Security;

public class MfaEnforcementPolicyTests
{
    [Fact]
    public async Task IsPrivilegedMfaRequiredAsync_ShouldBeFalse_InDevelopment()
    {
        var policy = new MfaEnforcementPolicy(
            CreateEnvironment(Environments.Development),
            CreateConfiguration(),
            CreateSystemSettings(enforce: true).Object);

        Assert.False(await policy.IsPrivilegedMfaRequiredAsync());
    }

    [Fact]
    public async Task IsPrivilegedMfaRequiredAsync_ShouldBeFalse_WhenE2EBypassEnabled()
    {
        var policy = new MfaEnforcementPolicy(
            CreateEnvironment("E2E"),
            CreateConfiguration(bypassE2E: true),
            CreateSystemSettings(enforce: true).Object);

        Assert.False(await policy.IsPrivilegedMfaRequiredAsync());
    }

    [Fact]
    public async Task IsPrivilegedMfaRequiredAsync_ShouldBeTrue_InNonSelfHostProduction()
    {
        var policy = new MfaEnforcementPolicy(
            CreateEnvironment("Production"),
            CreateConfiguration(),
            CreateSystemSettings(enforce: false).Object);

        Assert.True(await policy.IsPrivilegedMfaRequiredAsync());
    }

    [Fact]
    public async Task IsPrivilegedMfaRequiredAsync_ShouldBeFalse_InSelfHostWhenSettingDisabled()
    {
        var policy = new MfaEnforcementPolicy(
            CreateEnvironment("SelfHost"),
            CreateConfiguration(),
            CreateSystemSettings(enforce: false).Object);

        Assert.False(await policy.IsPrivilegedMfaRequiredAsync());
    }

    [Fact]
    public async Task IsPrivilegedMfaRequiredAsync_ShouldBeTrue_InSelfHostWhenSettingEnabled()
    {
        var policy = new MfaEnforcementPolicy(
            CreateEnvironment("SelfHost"),
            CreateConfiguration(),
            CreateSystemSettings(enforce: true).Object);

        Assert.True(await policy.IsPrivilegedMfaRequiredAsync());
    }

    private static Mock<ISystemSettingsService> CreateSystemSettings(bool enforce)
    {
        var mock = new Mock<ISystemSettingsService>();
        mock.Setup(x => x.GetBoolAsync(SystemSettingKeys.EnforcePrivilegedMfa, It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(enforce);
        return mock;
    }

    private static IConfiguration CreateConfiguration(bool bypassE2E = false)
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["E2E:BypassPrivilegedMfa"] = bypassE2E ? "true" : "false"
            })
            .Build();
    }

    private static IHostEnvironment CreateEnvironment(string environmentName)
    {
        var environmentMock = new Mock<IHostEnvironment>();
        environmentMock.SetupGet(x => x.EnvironmentName).Returns(environmentName);
        return environmentMock.Object;
    }
}
