using System.Reflection;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.AspNetCore.Mvc.Controllers;
using ProjectK.API.Controllers.DevModule;

namespace ProjectK.API.Hosting;

/// <summary>
/// Takes the dev tools controller out of the application model. Registered on every tier that is
/// not one of <see cref="Tiers"/>, so that a deployed instance has no such routes at all — the
/// role switcher signs people in without a password, and that must never be a question of one
/// missing attribute.
/// </summary>
public sealed class DevOnlyControllerFeatureProvider : IApplicationFeatureProvider<ControllerFeature>
{
    /// <summary>Where the dev tools exist: the tiers that are wiped or shown to testers, never to a live kurin.</summary>
    public static readonly string[] Tiers = ["Development", "E2E", "Tailscale"];

    public static bool Allows(string environmentName) => Tiers.Contains(environmentName, StringComparer.OrdinalIgnoreCase);

    public void PopulateFeature(IEnumerable<ApplicationPart> parts, ControllerFeature feature)
    {
        feature.Controllers.Remove(typeof(DevToolsController).GetTypeInfo());
    }
}
