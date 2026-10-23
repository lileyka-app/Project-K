using FluentAssertions;
using ProjectK.BusinessLogic.Modules.MeModule.Features.Duties;
using ProjectK.BusinessLogic.Tests.TestHelpers;
using ProjectK.Common.Models.Authorization;
using ProjectK.Common.Models.Authorization.KurinPolicy;
using ProjectK.Common.Models.Enums;
using Xunit;

namespace ProjectK.BusinessLogic.Tests.MeModule;

/// <summary>Whose queue a duty lands in: the офіси of one kurin, read exactly as the token would read them.</summary>
public class DutyRightsTests
{
    private static readonly Guid Led = Guid.NewGuid();
    private static readonly Guid Other = Guid.NewGuid();

    private static DutyRights Of(LeadershipType type, LeadershipRole role, params Guid[] led) =>
        new(RolePermissionMap.Resolve([SystemRole.ForOffice(type, role)]), led.ToHashSet());

    [Fact]
    public void TheZvyazkovyi_SeesEverything_InTheKurin()
    {
        var rights = Of(LeadershipType.KV, LeadershipRole.Zvyazkovyi);

        rights.WholeKurin(ResourceType.BadgeProgress, ResourceAction.Update).Should().BeTrue();
        rights.WholeKurin(ResourceType.KurinDues, ResourceAction.Update).Should().BeTrue();
        rights.ForGroup(ResourceType.GroupDues, ResourceAction.Update, Other).Should().BeTrue();
        rights.ForGroup(ResourceType.GroupScore, ResourceAction.Create, null).Should().BeTrue();
    }

    [Fact]
    public void AGroupSkarbnyk_SeesOnlyTheirOwnBox()
    {
        var rights = Of(LeadershipType.Group, LeadershipRole.Skarbnyk, Led);

        rights.ForGroup(ResourceType.GroupDues, ResourceAction.Update, Led).Should().BeTrue();
        rights.ForGroup(ResourceType.GroupDues, ResourceAction.Update, Other).Should().BeFalse();
        rights.WholeKurin(ResourceType.KurinDues, ResourceAction.Update).Should().BeFalse();
        rights.ForAnyGroup(ResourceType.BadgeProgress, ResourceAction.Update).Should().BeFalse();
        rights.ForAnyGroup(ResourceType.GroupScore, ResourceAction.Create).Should().BeFalse();
    }

    [Fact]
    public void AGroupSuddya_ScoresTheirGurtok_AndNothingElse()
    {
        var rights = Of(LeadershipType.Group, LeadershipRole.Suddya, Led);

        rights.ForAnyGroup(ResourceType.GroupScore, ResourceAction.Create).Should().BeTrue();
        rights.ForGroup(ResourceType.GroupScore, ResourceAction.Create, Other).Should().BeFalse();
        rights.ForAnyGroup(ResourceType.GroupDues, ResourceAction.Update).Should().BeFalse();
    }

    // The office is held, but no гурток is led: the OwnGroups scope reaches nothing.
    [Fact]
    public void AnOwnGroupsRight_WithNoGurtokLed_ReachesNothing() =>
        Of(LeadershipType.Group, LeadershipRole.Suddya).ForAnyGroup(ResourceType.GroupScore, ResourceAction.Create).Should().BeFalse();

    [Fact]
    public void AYouth_HasNoDuties()
    {
        var rights = new DutyRights([], new HashSet<Guid>());

        rights.ForAnyGroup(ResourceType.BadgeProgress, ResourceAction.Update).Should().BeFalse();
        rights.ForAnyGroup(ResourceType.GroupDues, ResourceAction.Update).Should().BeFalse();
        rights.ForAnyGroup(ResourceType.GroupScore, ResourceAction.Create).Should().BeFalse();
    }

    // The duty follows the kurin's policy: a kurin that keeps вмілості from впорядники keeps the queue from them too.
    [Fact]
    public void ABadgeDuty_FollowsTheKurinsPolicy()
    {
        var policy = PolicyAccess.With((KurinCapability.ReviewBadge, PolicyAudience.Vykhovnyk, CapabilityReach.None));
        var rights = new DutyRights(RolePermissionMap.Resolve([PolicyAccess.Vykhovnyk], policy), new HashSet<Guid> { Led });

        rights.ForAnyGroup(ResourceType.BadgeProgress, ResourceAction.Update).Should().BeFalse();
        Of(LeadershipType.KV, LeadershipRole.Vykhovnyk, Led).ForGroup(ResourceType.BadgeProgress, ResourceAction.Update, Led).Should().BeTrue();
    }
}
