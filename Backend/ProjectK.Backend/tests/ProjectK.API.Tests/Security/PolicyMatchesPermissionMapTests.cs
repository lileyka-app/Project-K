using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using ProjectK.API.Authorization;
using ProjectK.API.Controllers.KurinModule;
using ProjectK.API.Helpers;
using ProjectK.Common.Models.Authorization;
using ProjectK.Common.Models.Enums;

namespace ProjectK.API.Tests.Security;

/// <summary>
/// Two gates guard most endpoints: a coarse policy ("is the caller this kind of office") and the
/// resource check ("may this caller act on this record"). They are written independently, per
/// endpoint, so they drift — and when the policy refuses an office the permission map allows, the
/// account gets a 403 nothing else explains.
/// <para>
/// The rule pinned here is the one that broke: <b>reading a record must never require more than
/// changing it.</b> <c>GetLeadershipByKeyQuery</c> demanded whole-kurin management while
/// <c>UpdateLeadership</c> asked only for a signed-in user, so Курінний — who may seat the offices
/// below him — could not open the record he was allowed to edit. The edit page died on its first
/// request and every existing test stayed green.
/// </para>
/// <para>
/// Compared over what a caller <b>effectively</b> gets — the offices the policy admits intersected
/// with the offices holding the permission — because that is what the request meets: both gates run.
/// Comparing the policies alone reads a bare <c>RequireUser</c> as "everyone", when the resource
/// check behind it may allow exactly one office.
/// </para>
/// </summary>
public class PolicyMatchesPermissionMapTests
{
    private static readonly ResourceAction[] WriteActions =
        [ResourceAction.Create, ResourceAction.Update, ResourceAction.Delete, ResourceAction.Manage];

    [Fact]
    public void ReadingAResource_ShouldNeverRequireMoreThanChangingIt()
    {
        var endpoints = ControllerActions()
            .SelectMany(action => StaticResourceChecks(action)
                .Select(check => new
                {
                    Action = action,
                    check.Resource,
                    check.ResourceAction,
                    Policy = PolicyOf(action)
                }))
            .Where(endpoint => endpoint.Policy != null)
            .ToList();

        var offences = new List<string>();

        foreach (var read in endpoints.Where(endpoint => endpoint.ResourceAction == ResourceAction.Read))
        {
            var readers = EffectiveOffices(read.Policy!, read.Resource, read.ResourceAction);

            foreach (var write in endpoints.Where(endpoint =>
                         endpoint.Resource == read.Resource && WriteActions.Contains(endpoint.ResourceAction)))
            {
                var writers = EffectiveOffices(write.Policy!, write.Resource, write.ResourceAction);
                var refused = writers.Except(readers).ToList();

                if (refused.Count > 0)
                {
                    offences.Add(
                        $"{Name(read.Action)} reads {read.Resource} behind {read.Policy}, but "
                        + $"{Name(write.Action)} lets {string.Join(", ", refused)} change it — "
                        + "they may write what they cannot read.");
                }
            }
        }

        Assert.True(offences.Count == 0, string.Join(Environment.NewLine, offences));
    }

    /// <summary>
    /// The offices that actually get through both gates: admitted by the policy and holding the
    /// permission at some scope. Admin is excluded — it passes everything by definition.
    /// </summary>
    private static IReadOnlyCollection<string> EffectiveOffices(
        string policy,
        ResourceType resource,
        ResourceAction action)
    {
        Func<IEnumerable<string>, bool> admits = policy switch
        {
            AuthorizationPolicies.RequireKurinManagement => RolePermissionMap.GrantsWholeKurinManagement,
            AuthorizationPolicies.RequireGroupLeadership => RolePermissionMap.GrantsGroupLeadership,
            AuthorizationPolicies.RequirePlanningAuthor => RolePermissionMap.GrantsPlanningAuthoring,
            AuthorizationPolicies.RequireUser => _ => true,
            _ => _ => false
        };

        return SystemRole.All()
            .Where(role => role != SystemRole.Admin)
            .Where(role => admits([role]))
            .Where(role => RolePermissionMap.WidestScope(
                RolePermissionMap.Resolve([role]), resource, action) is not null)
            .ToList();
    }

    private static string Name(MethodInfo action) => $"{action.DeclaringType!.Name}.{action.Name}";

    private static string? PolicyOf(MethodInfo action) =>
        (action.GetCustomAttribute<AuthorizeAttribute>()
         ?? action.DeclaringType?.GetCustomAttribute<AuthorizeAttribute>())?.Policy;

    private static IEnumerable<MethodInfo> ControllerActions() =>
        typeof(LeadershipController).Assembly
            .GetTypes()
            .Where(type => typeof(ControllerBase).IsAssignableFrom(type) && !type.IsAbstract)
            .SelectMany(type => type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            .Where(method => method.GetCustomAttributes<HttpMethodAttribute>().Any())
            .Where(method => method.GetCustomAttribute<AllowAnonymousAttribute>() == null);

    /// <summary>
    /// The (resource, action) pairs an endpoint checks, for the attributes naming the resource
    /// outright. The selector form resolves its type from the request, so there is nothing static to
    /// compare.
    /// </summary>
    private static IEnumerable<(ResourceType Resource, ResourceAction ResourceAction)> StaticResourceChecks(
        MethodInfo action)
    {
        foreach (var attribute in action.GetCustomAttributes<ResourceAuthorizeAttribute>())
        {
            if (attribute.Arguments is
                [bool hasStaticType, ResourceType resource, _, ResourceAction resourceAction, ..] && hasStaticType)
            {
                yield return (resource, resourceAction);
            }
        }
    }
}
