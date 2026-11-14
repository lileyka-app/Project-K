using ProjectK.Common.Enums.AuthModule;
using ProjectK.Common.Rules.AuthModule;

namespace ProjectK.API.Tests.Security;

/// <summary>
/// Who may touch the вкладка. Money is the one thing kept out of the
/// read-everything baseline, so a gap here shows every youth the гурток's debts.
/// </summary>
public class DuesGrantsTests
{
    private static AccessScope? Widest(string role, ResourceType resource, ResourceAction action) =>
        RolePermissionMap.WidestScope(RolePermissionMap.Resolve([role]), resource, action);

    [Theory]
    [InlineData("Member")]
    [InlineData("Group.Pysar")]
    [InlineData("Kurin.Kurinnuy")]
    public void OthersSeeOnlyTheirOwnBalance_AndNoBox(string role)
    {
        Assert.Equal(AccessScope.Own, Widest(role, ResourceType.GroupDues, ResourceAction.Read));
        Assert.Null(Widest(role, ResourceType.GroupDues, ResourceAction.Create));
        Assert.Null(Widest(role, ResourceType.KurinDues, ResourceAction.Read));
    }

    [Theory]
    [InlineData("Group.Skarbnyk")]
    [InlineData("Group.Hurtkoviy")]
    [InlineData("KV.Vykhovnyk")]
    [InlineData("KV.Instruktor")]
    public void GroupKeepers_KeepTheirOwnGroupsBox(string role)
    {
        foreach (var action in new[] { ResourceAction.Read, ResourceAction.Create, ResourceAction.Update, ResourceAction.Delete })
        {
            Assert.Equal(AccessScope.OwnGroups, Widest(role, ResourceType.GroupDues, action));
        }

        Assert.Null(Widest(role, ResourceType.KurinDues, ResourceAction.Read));
    }

    [Theory]
    [InlineData("KV.Vykhovnyk", AccessScope.OwnGroups)]
    [InlineData("KV.Instruktor", AccessScope.OwnGroups)]
    [InlineData("KV.Zvyazkovyi", AccessScope.KurinWide)]
    [InlineData("Group.Skarbnyk", null)]
    [InlineData("Group.Hurtkoviy", null)]
    [InlineData("Kurin.Skarbnyk", null)]
    public void OnlyTheVykhovnykAndTheZvyazkovyiVerify(string role, AccessScope? expected)
    {
        Assert.Equal(expected, Widest(role, ResourceType.GroupDues, ResourceAction.Manage));
    }

    [Fact]
    public void KurinSkarbnyk_KeepsTheKurinBox_AndReadsEveryGroupsBox()
    {
        const string role = "Kurin.Skarbnyk";

        Assert.Equal(AccessScope.KurinWide, Widest(role, ResourceType.KurinDues, ResourceAction.Update));
        Assert.Equal(AccessScope.KurinWide, Widest(role, ResourceType.GroupDues, ResourceAction.Read));
        Assert.Null(Widest(role, ResourceType.GroupDues, ResourceAction.Create));
        Assert.Null(Widest(role, ResourceType.KurinDues, ResourceAction.Manage));
    }

    [Fact]
    public void Zvyazkovyi_HasAllOfIt()
    {
        foreach (var resource in new[] { ResourceType.GroupDues, ResourceType.KurinDues })
        {
            foreach (var action in Enum.GetValues<ResourceAction>())
            {
                Assert.Equal(AccessScope.KurinWide, Widest("KV.Zvyazkovyi", resource, action));
            }
        }
    }
}
