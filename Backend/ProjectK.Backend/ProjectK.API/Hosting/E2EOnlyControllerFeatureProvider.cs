using System.Reflection;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.AspNetCore.Mvc.Controllers;
using ProjectK.API.Controllers.TestModule;

namespace ProjectK.API.Hosting;

/// <summary>
/// Takes the e2e fixture controller out of the application model. Registered in every environment
/// except E2E, so that outside the test stack its routes are absent rather than answered by the
/// controller's own environment check.
/// </summary>
public sealed class E2EOnlyControllerFeatureProvider : IApplicationFeatureProvider<ControllerFeature>
{
    public void PopulateFeature(IEnumerable<ApplicationPart> parts, ControllerFeature feature)
    {
        feature.Controllers.Remove(typeof(E2ETestController).GetTypeInfo());
    }
}
