using Moq;
using ProjectK.API.Tests.TestHelpers;
using ProjectK.BusinessLogic.Modules.IdentityModule.Services;
using ProjectK.BusinessLogic.Services.Caching;
using ProjectK.Common.Contracts.IdentityModule;
using ProjectK.Common.Contracts.NotificationsModule;
using ProjectK.Common.Contracts.Shared;
using ProjectK.Common.Enums.IdentityModule;
using ProjectK.Common.Enums.KurinModule;
using ProjectK.Common.Records.NotificationsModule;
using ProjectK.Common.Rules.IdentityModule;

namespace ProjectK.API.Tests.Security;

/// <summary>
/// The youth record of someone who stands in a УСП or УПС kurin (LIFE-01): only who holds the
/// kurin-wide right where they stand now changes it, the person never enters their own вмілості or
/// УПЮ відзначення (their ступені they do), and probes nobody changes. A youth in an УПЮ kurin is
/// worked on as always.
/// </summary>
public class SeniorYouthRecordAccessTests
{
    private static readonly string Zvyazkovyi = SystemRoleNames.ForOffice(LeadershipType.KV, LeadershipRole.Zvyazkovyi);
    private static readonly string Vykhovnyk = SystemRoleNames.ForOffice(LeadershipType.KV, LeadershipRole.Vykhovnyk);

    private readonly Guid _kurinKey = Guid.NewGuid();
    private readonly Guid _groupKey = Guid.NewGuid();
    private readonly Guid _memberKey = Guid.NewGuid();
    private readonly Guid _ownerUserKey = Guid.NewGuid();

    private (ResourceAccessService Service, Mock<IResourceScopeRepository> Reader) Arrange(string role, bool senior, Guid? callerUserKey = null, bool inKurin = true)
    {
        var userKey = callerUserKey ?? Guid.NewGuid();
        var user = new Mock<ICurrentUserContext>();
        user.SetupGet(x => x.IsAuthenticated).Returns(true);
        user.SetupGet(x => x.KurinKey).Returns(_kurinKey);
        user.SetupGet(x => x.UserId).Returns(userKey);
        user.SetupGet(x => x.Roles).Returns([role]);
        user.Setup(x => x.IsInRole(It.IsAny<string>())).Returns((string candidate) => candidate == role);

        var reader = ScopeFacts.Repository();
        reader.Setup(x => x.GetScopeAsync(ResourceType.Member, _memberKey, It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(inKurin ? new ResourceScope(_kurinKey, _groupKey, _ownerUserKey) : null);
        reader.Setup(x => x.GetPersonAccountKeyAsync(ResourceType.Member, _memberKey, It.IsAny<CancellationToken>())).ReturnsAsync(_ownerUserKey);
        reader.Setup(x => x.GetLedGroupSourcesAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(ScopeFacts.Leading([_groupKey]));
        reader.Setup(x => x.GetOpenMembershipBranchesAsync(It.IsAny<ResourceType>(), _memberKey, It.IsAny<CancellationToken>())).ReturnsAsync(ScopeFacts.Standing(senior));

        var service = new ResourceAccessService(Mock.Of<IUnitOfWork>(u => u.ResourceScopes == reader.Object), user.Object, new PassThroughCache(), new CurrentPermissions(user.Object, new FixedKurinAccessPolicyProvider()));
        return (service, reader);
    }

    [Fact]
    public async Task TheZvyazkovyiWhereTheSeniorStands_ChangesTheirVmilosti()
    {
        var (service, _) = Arrange(Zvyazkovyi, senior: true);

        Assert.True((await service.CheckAccessAsync(ResourceType.BadgeProgress, ResourceAction.Create, ResourceType.Member, _memberKey)).IsAllowed);
        Assert.True((await service.CheckAccessAsync(ResourceType.BadgeProgress, ResourceAction.Update, ResourceType.Member, _memberKey)).IsAllowed);
        Assert.True((await service.CheckSeniorYouthRecordAsync(ResourceType.MemberAward, ResourceAction.Create, _memberKey)).IsAllowed);
    }

    [Fact]
    public async Task AGroupScopedLeader_DoesNotChangeASeniorsYouthRecord()
    {
        // A ст. пл. who is still a впорядник in an УПЮ kurin stands in its гурток's reach.
        var (service, _) = Arrange(Vykhovnyk, senior: true);

        var badge = await service.CheckAccessAsync(ResourceType.BadgeProgress, ResourceAction.Update, ResourceType.Member, _memberKey);
        var award = await service.CheckSeniorYouthRecordAsync(ResourceType.MemberAward, ResourceAction.Create, _memberKey);

        Assert.False(badge.IsAllowed);
        Assert.Contains("kurin-wide", badge.Reason);
        Assert.False(award.IsAllowed);
    }

    [Fact]
    public async Task ALeaderOfTheFormerKurin_DoesNotReachTheSeniorAtAll()
    {
        var (service, _) = Arrange(Zvyazkovyi, senior: true, inKurin: false);

        Assert.False((await service.CheckAccessAsync(ResourceType.BadgeProgress, ResourceAction.Update, ResourceType.Member, _memberKey)).IsAllowed);
    }

    [Fact]
    public async Task NobodyChangesASeniorsProbes_NotEvenTheZvyazkovyi()
    {
        var (service, _) = Arrange(Zvyazkovyi, senior: true);

        var sign = await service.CheckAccessAsync(ResourceType.ProbeProgress, ResourceAction.Update, ResourceType.Member, _memberKey);
        var unsign = await service.CheckCapabilityAsync(KurinCapability.UnsignProbe, ResourceType.Member, _memberKey);
        var reachable = await service.ReachableCapabilitiesAsync(ResourceType.Member, _memberKey);

        Assert.False(sign.IsAllowed);
        Assert.Contains("archive", sign.Reason);
        Assert.False(unsign.IsAllowed);
        Assert.DoesNotContain(KurinCapability.SignProbe, reachable);
        Assert.Contains(KurinCapability.ReviewBadge, reachable);
    }

    [Fact]
    public async Task TheSeniorThemself_EntersTheirStupeni_ButNotTheirVmilostiOrYouthAwards()
    {
        var (service, _) = Arrange(SystemRoleNames.Member, senior: true, callerUserKey: _ownerUserKey);

        var badge = await service.CheckAccessAsync(ResourceType.BadgeProgress, ResourceAction.Create, ResourceType.Member, _memberKey);
        var award = await service.CheckSeniorYouthRecordAsync(ResourceType.MemberAward, ResourceAction.Create, _memberKey);
        var levels = await service.CheckSeniorYouthRecordAsync(ResourceType.Member, ResourceAction.Update, _memberKey, ownerMayChange: true);

        Assert.False(badge.IsAllowed);
        Assert.Contains("own youth record", badge.Reason);
        Assert.False(award.IsAllowed);
        Assert.True(levels.IsAllowed);
    }

    [Fact]
    public async Task InAnUpyuKurin_TheGurtoksProvid_WorksOnItsYouthAsAlways()
    {
        var (service, _) = Arrange(Vykhovnyk, senior: false);

        Assert.True((await service.CheckAccessAsync(ResourceType.ProbeProgress, ResourceAction.Update, ResourceType.Member, _memberKey)).IsAllowed);
        Assert.True((await service.CheckAccessAsync(ResourceType.BadgeProgress, ResourceAction.Update, ResourceType.Member, _memberKey)).IsAllowed);
        Assert.True((await service.CheckSeniorYouthRecordAsync(ResourceType.MemberAward, ResourceAction.Create, _memberKey)).IsAllowed);
        Assert.Contains(KurinCapability.SignProbe, await service.ReachableCapabilitiesAsync(ResourceType.Member, _memberKey));
    }

    [Fact]
    public async Task AYouth_StillSubmitsTheirOwnVmilist()
    {
        var (service, _) = Arrange(SystemRoleNames.Member, senior: false, callerUserKey: _ownerUserKey);

        Assert.True((await service.CheckAccessAsync(ResourceType.BadgeProgress, ResourceAction.Create, ResourceType.Member, _memberKey)).IsAllowed);
    }

    [Fact]
    public async Task ConfirmingASeniorsAward_GoesByThatLevelsReach_NotTheWidestOfTheFour()
    {
        // A kurin may give level 1 to the whole kurin and level 4 only within a гурток.
        var user = new Mock<ICurrentUserContext>();
        user.SetupGet(x => x.IsAuthenticated).Returns(true);
        user.SetupGet(x => x.KurinKey).Returns(_kurinKey);
        user.SetupGet(x => x.UserId).Returns(Guid.NewGuid());
        user.SetupGet(x => x.Roles).Returns([Vykhovnyk]);
        var permissions = new Mock<ICurrentPermissions>();
        permissions.Setup(p => p.WidestScopeAsync(ResourceType.MemberAward, ResourceAction.Update, It.IsAny<CancellationToken>())).ReturnsAsync(AccessScope.KurinWide);
        permissions.Setup(p => p.ReachAsync(KurinCapability.ConfirmAward1, It.IsAny<CancellationToken>())).ReturnsAsync(CapabilityReach.Kurin);
        permissions.Setup(p => p.ReachAsync(KurinCapability.ConfirmAward4, It.IsAny<CancellationToken>())).ReturnsAsync(CapabilityReach.OwnGroup);
        var reader = ScopeFacts.Repository();
        reader.Setup(x => x.GetOpenMembershipBranchesAsync(It.IsAny<ResourceType>(), _memberKey, It.IsAny<CancellationToken>())).ReturnsAsync(ScopeFacts.Standing(true));
        var service = new ResourceAccessService(Mock.Of<IUnitOfWork>(u => u.ResourceScopes == reader.Object), user.Object, new PassThroughCache(), permissions.Object);

        var first = await service.CheckSeniorYouthRecordAsync(ResourceType.MemberAward, ResourceAction.Update, _memberKey, capability: KurinCapability.ConfirmAward1);
        var fourth = await service.CheckSeniorYouthRecordAsync(ResourceType.MemberAward, ResourceAction.Update, _memberKey, capability: KurinCapability.ConfirmAward4);

        Assert.True(first.IsAllowed);
        Assert.False(fourth.IsAllowed);
    }

    private sealed class PassThroughCache : IBackendCache
    {
        public Task<T> GetOrCreateAsync<T>(CachePolicy policy, string key, Func<CancellationToken, Task<T>> factory, CancellationToken cancellationToken, CacheScopeContext? scopeContext = null)
            => factory(cancellationToken);

        public void Invalidate(CachePolicy policy) { }

        public void InvalidateByPrefix(string prefix) { }
    }
}
