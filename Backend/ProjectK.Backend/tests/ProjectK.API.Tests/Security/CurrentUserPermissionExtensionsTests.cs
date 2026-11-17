using Moq;
using ProjectK.Common.Contracts.InfrastructureModule;
using ProjectK.Common.Enums.IdentityModule;
using ProjectK.Common.Enums.MemberModule;
using ProjectK.Common.Rules.IdentityModule;

namespace ProjectK.API.Tests.Security;

/// <summary>
/// The synchronous helpers know only the static map. Asked about a pair the kurin's policy decides,
/// they would answer for a kurin that never changed anything — so they refuse to answer at all.
/// </summary>
public class CurrentUserPermissionExtensionsTests
{
    private static ICurrentUserContext Zvyazkovyi()
    {
        var user = new Mock<ICurrentUserContext>();
        user.SetupGet(x => x.Roles).Returns([SystemRoleNames.Member, SystemRoleNames.ForOffice(LeadershipType.KV, LeadershipRole.Zvyazkovyi)]);
        return user.Object;
    }

    public static TheoryData<ResourceType, ResourceAction> ConfigurablePairs()
    {
        var data = new TheoryData<ResourceType, ResourceAction>();
        foreach (var pair in KurinCapabilityRules.All.Select(descriptor => (descriptor.Resource, descriptor.Action)).Distinct())
        {
            data.Add(pair.Resource, pair.Action);
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(ConfigurablePairs))]
    public void HasPermission_ShouldThrow_ForAPairTheKurinsPolicyDecides(ResourceType resource, ResourceAction action)
    {
        Assert.Throws<ArgumentException>(() => Zvyazkovyi().HasPermission(resource, action));
    }

    [Fact]
    public void HasPermission_ShouldAnswer_ForAStaticPair()
    {
        Assert.True(Zvyazkovyi().HasPermission(ResourceType.PlanningSession, ResourceAction.Create));
        Assert.False(Zvyazkovyi().HasPermission(ResourceType.Kurin, ResourceAction.Delete));
    }

    [Fact]
    public void StaticPermissions_ShouldHoldNoPolicyPair_BeyondTheOwnGrants()
    {
        var offending = Zvyazkovyi().StaticPermissions()
            .Where(permission => permission.Scope != AccessScope.Own
                                 && KurinCapabilityRules.IsConfigurable(permission.Resource, permission.Action))
            .ToList();

        Assert.Empty(offending);
    }
}
