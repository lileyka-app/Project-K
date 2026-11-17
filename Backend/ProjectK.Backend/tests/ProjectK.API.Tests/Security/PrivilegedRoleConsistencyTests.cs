using ProjectK.Common.Enums.MemberModule;
using ProjectK.Common.Rules.IdentityModule;

namespace ProjectK.API.Tests.Security;

/// <summary>
/// Pins "who counts as privileged" to a single derived answer. A second hardcoded list used to live
/// on <see cref="SystemRoleNames"/> and drifted from the grant tables, so the MFA middleware never
/// required Курінний to enable MFA while the disable handler refused to let him turn it off.
/// </summary>
public class PrivilegedRoleConsistencyTests
{
    [Theory]
    [InlineData("Admin", true)]
    [InlineData("KV.Zvyazkovyi", true)]
    [InlineData("Kurin.Kurinnuy", false)]
    [InlineData("Group.Hurtkoviy", false)]
    [InlineData("KV.Vykhovnyk", false)]
    [InlineData("KV.Instruktor", false)]
    [InlineData("Kurin.Pysar", false)]
    [InlineData("Member", false)]
    public void GrantsWholeKurinManagement_ShouldMatchTheGrantTables(string role, bool expected)
    {
        Assert.Equal(expected, RolePermissionRules.GrantsWholeKurinManagement(new[] { role }));
    }

    [Fact]
    public void EveryOffice_ShouldResolveThroughTheMapWithoutASeparateList()
    {
        // Guards against a helper reintroducing a parallel "privileged roles" array: the only offices
        // that may answer true are the ones the grant tables give kurin-wide Group:Manage.
        var privileged = LeadershipOfficeRules.All()
            .Select(office => SystemRoleNames.ForOffice(office.Type, office.Role))
            .Where(role => RolePermissionRules.GrantsWholeKurinManagement(new[] { role }))
            .ToList();

        Assert.Equal(new[] { SystemRoleNames.ForOffice(LeadershipType.KV, LeadershipRole.Zvyazkovyi) }, privileged);
    }
}
