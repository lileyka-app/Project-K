using ProjectK.API.Controllers.KurinModule;
using ProjectK.API.Filters;
using ProjectK.Common.Models.Authorization;
using ProjectK.Common.Models.Enums;

namespace ProjectK.API.Tests.Security;

/// <summary>
/// Who opens the kurin's settings: the whole КВ reads them, «Права й видимість» included; only the
/// Звʼязковий changes them. The rest of the kurin, провід included, does not read them at all.
/// </summary>
public class KurinSettingsGrantsTests
{
    private static AccessScope? Widest(string role, ResourceType resource, ResourceAction action) =>
        RolePermissionMap.WidestScope(RolePermissionMap.Resolve([role]), resource, action);

    [Theory]
    [InlineData("KV.Zvyazkovyi")]
    [InlineData("KV.Vykhovnyk")]
    [InlineData("KV.Instruktor")]
    public void TheKV_ReadsTheSettings(string role)
    {
        Assert.Equal(AccessScope.KurinWide, Widest(role, ResourceType.KurinSettings, ResourceAction.Read));
    }

    [Theory]
    [InlineData("KV.Vykhovnyk")]
    [InlineData("KV.Instruktor")]
    public void OnlyTheZvyazkovyi_ChangesThem(string role)
    {
        Assert.Null(Widest(role, ResourceType.Kurin, ResourceAction.Update));
        Assert.Equal(AccessScope.KurinWide, Widest("KV.Zvyazkovyi", ResourceType.Kurin, ResourceAction.Update));
    }

    // Every read of the settings page: a read the КВ is refused leaves its tile empty or wrong.
    [Theory]
    [InlineData(typeof(KurinSettingsController), nameof(KurinSettingsController.GetCardFields))]
    [InlineData(typeof(KurinSettingsController), nameof(KurinSettingsController.GetCardSharing))]
    [InlineData(typeof(KurinSettingsController), nameof(KurinSettingsController.GetAccessPolicy))]
    [InlineData(typeof(KurinBranchRequestsController), nameof(KurinBranchRequestsController.GetBranchRequest))]
    [InlineData(typeof(KurinTagsController), nameof(KurinTagsController.GetTagSettings))]
    public void TheSettingsReads_AskForKurinSettingsRead(Type controller, string action)
    {
        Assert.Equal((ResourceType.KurinSettings, ResourceAction.Read), Guard(controller, action));
    }

    [Theory]
    [InlineData(typeof(KurinBranchRequestsController), nameof(KurinBranchRequestsController.SubmitBranchRequest))]
    [InlineData(typeof(KurinTagsController), nameof(KurinTagsController.SetTagSettings))]
    public void TheirWrites_StillAskForKurinUpdate(Type controller, string action)
    {
        Assert.Equal((ResourceType.Kurin, ResourceAction.Update), Guard(controller, action));
    }

    private static (ResourceType, ResourceAction) Guard(Type controller, string action)
    {
        var attribute = controller.GetMethods()
            .Single(m => m.Name == action)
            .GetCustomAttributes(typeof(ResourceAuthorizeAttribute), inherit: true)
            .Cast<ResourceAuthorizeAttribute>()
            .Single();
        return ((ResourceType)attribute.Arguments![1]!, (ResourceAction)attribute.Arguments[3]!);
    }

    [Theory]
    [InlineData("Member")]
    [InlineData("Kurin.Kurinnuy")]
    [InlineData("Group.Hurtkoviy")]
    [InlineData("Kurin.Skarbnyk")]
    public void TheRest_DoNotRead(string role)
    {
        Assert.Null(Widest(role, ResourceType.KurinSettings, ResourceAction.Read));
    }
}
