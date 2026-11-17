using FluentAssertions;
using Moq;
using ProjectK.BusinessLogic.Modules.AgendaModule.Services;
using ProjectK.BusinessLogic.Modules.IdentityModule.Services;
using ProjectK.BusinessLogic.Modules.MemberModule.Services;
using ProjectK.BusinessLogic.Tests.TestHelpers;
using ProjectK.Common.Contracts.KurinModule;
using ProjectK.Common.Contracts.MemberModule;
using ProjectK.Common.Contracts.NotificationsModule;
using ProjectK.Common.Dtos.AgendaModule.Requests;
using ProjectK.Common.Enums.AgendaModule;
using ProjectK.Common.Enums.IdentityModule;
using ProjectK.Common.Enums.KurinModule;
using ProjectK.Common.Records.NotificationsModule;
using ProjectK.Common.Rules.IdentityModule;

namespace ProjectK.BusinessLogic.Tests.AgendaModule;

/// <summary>
/// Whom an agenda item may be assigned to, through the real decision: roles folded into the kurin's
/// policy columns, the Assign* rows, and the resource guard's scope check. Only the reads are faked —
/// one kurin with two гуртки, a person in each, each гурток's провід and the КВ; the user leads the first.
/// </summary>
public class AgendaAccessAuthorizeTargetTests
{
    private readonly Guid _kurinKey = Guid.NewGuid();
    private readonly Guid _ledGroup = Guid.NewGuid();
    private readonly Guid _otherGroup = Guid.NewGuid();
    private readonly Guid _inLedGroup = Guid.NewGuid();
    private readonly Guid _inOtherGroup = Guid.NewGuid();
    private readonly Guid _self = Guid.NewGuid();
    private readonly Guid _ledGroupProvid = Guid.NewGuid();
    private readonly Guid _otherGroupProvid = Guid.NewGuid();
    private readonly Guid _kv = Guid.NewGuid();
    private readonly Guid _elsewhereGroup = Guid.NewGuid();
    private readonly Guid _userKey = Guid.NewGuid();
    private readonly Guid _otherUserKey = Guid.NewGuid();

    private readonly FixedKurinAccessPolicyProvider _policies = new();
    private readonly Mock<IResourceScopeRepository> _scopes = ScopeFacts.Repository();

    public AgendaAccessAuthorizeTargetTests()
    {
        Scope(ResourceType.Kurin, _kurinKey, new ResourceScope(_kurinKey, null, null));
        Scope(ResourceType.Group, _ledGroup, new ResourceScope(_kurinKey, _ledGroup, null));
        Scope(ResourceType.Group, _otherGroup, new ResourceScope(_kurinKey, _otherGroup, null));
        Scope(ResourceType.Group, _elsewhereGroup, new ResourceScope(Guid.NewGuid(), _elsewhereGroup, null));
        Scope(ResourceType.Member, _inLedGroup, new ResourceScope(_kurinKey, _ledGroup, _otherUserKey));
        Scope(ResourceType.Member, _inOtherGroup, new ResourceScope(_kurinKey, _otherGroup, Guid.NewGuid()));
        Scope(ResourceType.Member, _self, new ResourceScope(_kurinKey, _otherGroup, _userKey));
        Scope(ResourceType.Leadership, _ledGroupProvid, new ResourceScope(_kurinKey, _ledGroup, null));
        Scope(ResourceType.Leadership, _otherGroupProvid, new ResourceScope(_kurinKey, _otherGroup, null));
        Scope(ResourceType.Leadership, _kv, new ResourceScope(_kurinKey, null, null));
        _scopes.Setup(x => x.GetLedGroupSourcesAsync(_userKey, _kurinKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ScopeFacts.Leading(new[] { _ledGroup }));
    }

    private readonly Mock<IKurinTagDirectory> _tags = new();

    private void Scope(ResourceType type, Guid key, ResourceScope scope) =>
        _scopes.Setup(x => x.GetScopeAsync(type, key, It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(scope);

    private AgendaAccess AccessFor(string role)
    {
        var roles = role == SystemRoleNames.Member ? new[] { SystemRoleNames.Member } : new[] { role, SystemRoleNames.Member };
        var user = new Mock<ICurrentUserContext>();
        user.SetupGet(x => x.IsAuthenticated).Returns(true);
        user.SetupGet(x => x.UserId).Returns(_userKey);
        user.SetupGet(x => x.KurinKey).Returns(_kurinKey);
        user.SetupGet(x => x.Roles).Returns(roles);
        user.Setup(x => x.IsInRole(It.IsAny<string>())).Returns((string r) => roles.Contains(r, StringComparer.OrdinalIgnoreCase));

        var permissions = new CurrentPermissions(user.Object, _policies);
        var resourceAccess = new ResourceAccessService(UnitOfWorks.WithScopes(_scopes.Object), user.Object, new PassThroughBackendCache(), permissions);
        return new AgendaAccess(user.Object, UnitOfWorks.WithScopes(_scopes.Object), new CurrentMember(user.Object, Mock.Of<IMemberDirectory>(), Mock.Of<IMembershipDirectory>()), resourceAccess, permissions, _tags.Object, Mock.Of<IOfficeDirectory>(), ScopeFacts.Reader(_scopes.Object));
    }

    private static AgendaTargetInput Target(AgendaTargetType type, Guid key) => new() { TargetType = type, TargetKey = key };

    /// <summary>The probes, in the order the expectation strings below spell them.</summary>
    private IReadOnlyList<(AgendaTargetInput Target, Guid? GroupKey, Guid? UserKey)> Probes() =>
    [
        (Target(AgendaTargetType.Kurin, _kurinKey), null, null),
        (Target(AgendaTargetType.Group, _ledGroup), _ledGroup, null),
        (Target(AgendaTargetType.Group, _otherGroup), _otherGroup, null),
        (Target(AgendaTargetType.Member, _inLedGroup), _ledGroup, _otherUserKey),
        (Target(AgendaTargetType.Member, _inOtherGroup), _otherGroup, null),
        (Target(AgendaTargetType.Leadership, _ledGroupProvid), _ledGroup, null),
        (Target(AgendaTargetType.Leadership, _otherGroupProvid), _otherGroup, null),
        (Target(AgendaTargetType.Leadership, _kv), null, null)
    ];

    private static readonly string Zvyazkovyi = SystemRoleNames.ForOffice(LeadershipType.KV, LeadershipRole.Zvyazkovyi);
    private static readonly string Vykhovnyk = SystemRoleNames.ForOffice(LeadershipType.KV, LeadershipRole.Vykhovnyk);
    private static readonly string Hurtkovyy = SystemRoleNames.ForOffice(LeadershipType.Group, LeadershipRole.Hurtkoviy);

    // Kurin · led гурток · other гурток · person in led · person in other · led провід · other провід · КВ.
    [Theory]
    [InlineData("KV.Zvyazkovyi", "YYYYYYYY")]
    [InlineData("Admin", "YYYYYYYY")]
    [InlineData("KV.Vykhovnyk", "-Y-Y-Y--")]
    [InlineData("KV.Instruktor", "-Y-Y-Y--")]
    [InlineData("Kurin.Kurinnuy", "YYYYYYYY")]
    [InlineData("Kurin.Pysar", "YYYYYYYY")]
    [InlineData("Kurin.Skarbnyk", "YYYYYYYY")]
    [InlineData("Group.Hurtkoviy", "-Y-Y-Y--")]
    [InlineData("Group.Pysar", "-Y-Y-Y--")]
    [InlineData("Member", "--------")]
    public async Task Defaults_DecideEveryKindOfTarget(string role, string expected)
    {
        var access = AccessFor(role);

        var actual = new List<char>();
        foreach (var probe in Probes())
        {
            actual.Add((await access.AuthorizeTargetAsync(probe.Target)).IsAllowed ? 'Y' : '-');
        }

        new string(actual.ToArray()).Should().Be(expected, $"{role} under the default policy");
    }

    /// <summary>A kurin with tags off takes no new tag target, even from someone the policy lets aim at tags.</summary>
    [Theory]
    [InlineData(true, true)]
    [InlineData(false, false)]
    public async Task ATagTarget_IsTakenOnlyWhileTheKurinUsesTags(bool tagsOn, bool allowed)
    {
        var tagKey = Guid.NewGuid();
        Scope(ResourceType.MemberTag, tagKey, new ResourceScope(_kurinKey, null, null));
        _tags.Setup(v => v.IsEnabledAsync(_kurinKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(tagsOn);

        var decision = await AccessFor("KV.Zvyazkovyi").AuthorizeTargetAsync(Target(AgendaTargetType.Tag, tagKey));

        decision.IsAllowed.Should().Be(allowed);
    }

    // The picker decides the same many targets from one reach; it must never disagree with the save.
    [Theory]
    [InlineData("KV.Zvyazkovyi")]
    [InlineData("KV.Vykhovnyk")]
    [InlineData("Kurin.Kurinnuy")]
    [InlineData("Group.Hurtkoviy")]
    [InlineData("Member")]
    public async Task TheReach_AgreesWithTheCheck_OnEveryTarget(string role)
    {
        _policies.Policy = PolicyAccess.With(
            (KurinCapability.AssignMembers, PolicyAudience.Kurinnyy, CapabilityReach.OwnGroup),
            (KurinCapability.AssignProvid, PolicyAudience.Hurtkovyy, CapabilityReach.None));
        var access = AccessFor(role);
        var reach = await access.GetAssignReachAsync(_kurinKey);

        foreach (var (target, groupKey, userKey) in Probes())
        {
            var byReach = target.TargetType switch
            {
                AgendaTargetType.Kurin => reach.ReachesKurin,
                AgendaTargetType.Group => reach.ReachesGroup(groupKey!.Value),
                AgendaTargetType.Member => reach.ReachesMember(groupKey, userKey),
                _ => reach.ReachesProvid(groupKey)
            };

            byReach.Should().Be((await access.AuthorizeTargetAsync(target)).IsAllowed, $"{role} → {target.TargetType} {target.TargetKey}");
        }
    }

    // The bug this replaced: the курінний was offered the проводи and refused them on save.
    [Fact]
    public async Task TheKurinny_MayAssignToEveryProvid_AndTheKv()
    {
        var access = AccessFor(SystemRoleNames.ForOffice(LeadershipType.Kurin, LeadershipRole.Kurinnuy));

        (await access.AuthorizeTargetAsync(Target(AgendaTargetType.Leadership, _kv))).IsAllowed.Should().BeTrue();
        (await access.AuthorizeTargetAsync(Target(AgendaTargetType.Leadership, _otherGroupProvid))).IsAllowed.Should().BeTrue();
    }

    [Fact]
    public async Task AKurin_TakingPeopleAwayFromTheVykhovnyk_IsObeyed()
    {
        _policies.Policy = PolicyAccess.With((KurinCapability.AssignMembers, PolicyAudience.Vykhovnyk, CapabilityReach.None));
        var access = AccessFor(Vykhovnyk);

        (await access.AuthorizeTargetAsync(Target(AgendaTargetType.Member, _inLedGroup))).IsAllowed.Should().BeFalse();
        (await access.AuthorizeTargetAsync(Target(AgendaTargetType.Group, _ledGroup))).IsAllowed.Should().BeTrue();
    }

    [Fact]
    public async Task AKurin_OpeningTheWholeKurinToTheHurtkovyy_IsObeyed()
    {
        _policies.Policy = PolicyAccess.With((KurinCapability.AssignKurin, PolicyAudience.Hurtkovyy, CapabilityReach.Kurin));
        var access = AccessFor(Hurtkovyy);

        (await access.AuthorizeTargetAsync(Target(AgendaTargetType.Kurin, _kurinKey))).IsAllowed.Should().BeTrue();
        (await access.AuthorizeTargetAsync(Target(AgendaTargetType.Group, _otherGroup))).IsAllowed.Should().BeFalse();
    }

    // An own-group grant always covers one's own record, as everywhere else.
    [Fact]
    public async Task AnOwnGroupReach_CoversTheUserHimself()
    {
        var access = AccessFor(Hurtkovyy);

        (await access.AuthorizeTargetAsync(Target(AgendaTargetType.Member, _self))).IsAllowed.Should().BeTrue();
    }

    [Fact]
    public async Task ATargetOutsideTheKurin_OrUnknown_IsRefused_EvenKurinWide()
    {
        var access = AccessFor(Zvyazkovyi);

        (await access.AuthorizeTargetAsync(Target(AgendaTargetType.Group, _elsewhereGroup))).IsAllowed.Should().BeFalse();
        (await access.AuthorizeTargetAsync(Target(AgendaTargetType.Kurin, Guid.NewGuid()))).IsAllowed.Should().BeFalse();
        (await access.AuthorizeTargetAsync(Target(AgendaTargetType.Leadership, Guid.NewGuid()))).IsAllowed.Should().BeFalse();
    }

    [Theory]
    [InlineData("Member", false)]
    [InlineData("Group.Pysar", true)]
    [InlineData("KV.Instruktor", true)]
    [InlineData("Kurin.Kurinnuy", true)]
    public async Task CanAuthor_FollowsTheDefaults(string role, bool expected)
    {
        (await AccessFor(role).CanAuthorAsync()).Should().Be(expected);
    }

    [Fact]
    public async Task CanAuthor_IsGone_OnceTheKurinTakesEveryRowAway()
    {
        _policies.Policy = PolicyAccess.With(
            (KurinCapability.AssignGroups, PolicyAudience.Hurtkovyy, CapabilityReach.None),
            (KurinCapability.AssignMembers, PolicyAudience.Hurtkovyy, CapabilityReach.None),
            (KurinCapability.AssignProvid, PolicyAudience.Hurtkovyy, CapabilityReach.None));
        var access = AccessFor(Hurtkovyy);

        (await access.CanAuthorAsync()).Should().BeFalse();
        (await access.GetAssignReachAsync(_kurinKey)).CanAuthor.Should().BeFalse();
    }
}
