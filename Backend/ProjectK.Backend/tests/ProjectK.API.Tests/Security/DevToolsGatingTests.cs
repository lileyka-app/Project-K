using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.AspNetCore.Mvc.Controllers;
using ProjectK.API.Authorization;
using ProjectK.API.Controllers.DevModule;
using ProjectK.API.Hosting;

namespace ProjectK.API.Tests.Security;

/// <summary>
/// The dev role switcher signs people in without a password. It exists on three local tiers and
/// nowhere else, and even there only an administrator can start it.
/// </summary>
public class DevToolsGatingTests
{
    [Theory]
    [InlineData("Development", true)]
    [InlineData("E2E", true)]
    [InlineData("Tailscale", true)]
    [InlineData("Staging", false)]
    [InlineData("Production", false)]
    [InlineData("SelfHost", false)]
    public void Allows_ShouldNameExactlyTheLocalTiers(string tier, bool expected)
    {
        Assert.Equal(expected, DevOnlyControllerFeatureProvider.Allows(tier));
    }

    [Fact]
    public void PopulateFeature_ShouldTakeTheControllerOutOfTheModel()
    {
        var feature = new ControllerFeature();
        feature.Controllers.Add(typeof(DevToolsController).GetTypeInfo());

        new DevOnlyControllerFeatureProvider().PopulateFeature(Array.Empty<ApplicationPart>(), feature);

        Assert.DoesNotContain(typeof(DevToolsController).GetTypeInfo(), feature.Controllers);
    }

    [Theory]
    [InlineData(nameof(DevToolsController.Impersonate))]
    [InlineData(nameof(DevToolsController.ImpersonateMember))]
    public void Impersonate_ShouldRequireAnAdministrator(string method)
    {
        var action = typeof(DevToolsController).GetMethod(method)!;
        var authorize = action.GetCustomAttribute<AuthorizeAttribute>();

        Assert.NotNull(authorize);
        Assert.Equal(AuthorizationPolicies.RequireAdmin, authorize!.Policy);
    }
}
