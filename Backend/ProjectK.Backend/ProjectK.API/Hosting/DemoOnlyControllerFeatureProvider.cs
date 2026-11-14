using System.Reflection;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.AspNetCore.Mvc.Controllers;
using ProjectK.API.Controllers.DemoModule;

namespace ProjectK.API.Hosting;

/// <summary>
/// Takes the demo entry controller out of the application model. Registered on every tier that is
/// not Demo, so that a password-less sign-in exists only where the data is a nightly-reset fixture.
/// </summary>
public sealed class DemoOnlyControllerFeatureProvider : IApplicationFeatureProvider<ControllerFeature>
{
    public const string Tier = "Demo";

    public static bool Allows(string environmentName) => string.Equals(environmentName, Tier, StringComparison.OrdinalIgnoreCase);

    public void PopulateFeature(IEnumerable<ApplicationPart> parts, ControllerFeature feature)
    {
        feature.Controllers.Remove(typeof(DemoController).GetTypeInfo());
    }
}
