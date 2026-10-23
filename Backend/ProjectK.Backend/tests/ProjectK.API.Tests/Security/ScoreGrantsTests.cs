using ProjectK.Common.Models.Authorization;
using ProjectK.Common.Models.Enums;

namespace ProjectK.API.Tests.Security;

/// <summary>
/// Who may touch точкування. The table of гуртки is everyone's; a
/// youth's points are his own and his гурток's провід's, so a gap here shows the whole kurin who
/// scored what.
/// </summary>
public class ScoreGrantsTests
{
    private static readonly ResourceAction[] Keeping =
        [ResourceAction.Read, ResourceAction.Create, ResourceAction.Update, ResourceAction.Delete];

    private static AccessScope? Widest(string role, ResourceType resource, ResourceAction action) =>
        RolePermissionMap.WidestScope(RolePermissionMap.Resolve([role]), resource, action);

    [Theory]
    [InlineData("Member")]
    [InlineData("Group.Pysar")]
    [InlineData("Kurin.Skarbnyk")]
    public void Others_SeeTheTable_AndOnlyTheirOwnPoints(string role)
    {
        Assert.Equal(AccessScope.KurinWide, Widest(role, ResourceType.KurinScore, ResourceAction.Read));
        Assert.Equal(AccessScope.Own, Widest(role, ResourceType.GroupScore, ResourceAction.Read));
        Assert.Null(Widest(role, ResourceType.GroupScore, ResourceAction.Create));
        Assert.Null(Widest(role, ResourceType.KurinScore, ResourceAction.Manage));
        Assert.Null(Widest(role, ResourceType.KurinScorePrivate, ResourceAction.Read));
    }

    [Theory]
    [InlineData("Group.Suddya")]
    [InlineData("Group.Hurtkoviy")]
    [InlineData("KV.Vykhovnyk")]
    [InlineData("KV.Instruktor")]
    public void TheGurtoksScorers_ScoreTheirOwnGurtok(string role)
    {
        foreach (var action in Keeping)
        {
            Assert.Equal(AccessScope.OwnGroups, Widest(role, ResourceType.GroupScore, action));
        }

        Assert.Null(Widest(role, ResourceType.KurinScore, ResourceAction.Manage));
    }

    [Theory]
    [InlineData("Kurin.Suddya")]
    [InlineData("Kurin.Kurinnuy")]
    [InlineData("KV.Zvyazkovyi")]
    public void TheKurinsScorers_ScoreEveryGurtok(string role)
    {
        foreach (var action in Keeping)
        {
            Assert.Equal(AccessScope.KurinWide, Widest(role, ResourceType.GroupScore, action));
        }
    }

    [Theory]
    [InlineData("Kurin.Suddya", AccessScope.KurinWide)]
    [InlineData("KV.Zvyazkovyi", AccessScope.KurinWide)]
    [InlineData("Kurin.Kurinnuy", null)]
    [InlineData("Group.Suddya", null)]
    [InlineData("KV.Vykhovnyk", null)]
    [InlineData("KV.Instruktor", null)]
    public void OnlyTheKurinsSuddyaAndTheZvyazkovyi_SetTheRules(string role, AccessScope? expected)
    {
        Assert.Equal(expected, Widest(role, ResourceType.KurinScore, ResourceAction.Manage));
    }

    [Theory]
    [InlineData("KV.Vykhovnyk", AccessScope.KurinWide)]
    [InlineData("KV.Instruktor", AccessScope.KurinWide)]
    [InlineData("KV.Zvyazkovyi", AccessScope.KurinWide)]
    [InlineData("Kurin.Suddya", null)]
    [InlineData("Kurin.Kurinnuy", null)]
    [InlineData("Member", null)]
    public void OnlyTheKV_KeepsItsPrivateScore(string role, AccessScope? expected)
    {
        foreach (var action in Keeping)
        {
            Assert.Equal(expected, Widest(role, ResourceType.KurinScorePrivate, action));
        }
    }

    // A суддя keeps everything the bare провід office had, the default assigning reach of his провід included.
    [Theory]
    [InlineData("Group.Suddya", AccessScope.OwnGroups)]
    [InlineData("Kurin.Suddya", AccessScope.KurinWide)]
    public void TheSuddya_KeepsHisProvidsGrants(string role, AccessScope planningScope)
    {
        Assert.Equal(planningScope, Widest(role, ResourceType.PlanningSession, ResourceAction.Create));
        Assert.Equal(planningScope, Widest(role, ResourceType.AgendaItem, ResourceAction.Create));
    }
}
