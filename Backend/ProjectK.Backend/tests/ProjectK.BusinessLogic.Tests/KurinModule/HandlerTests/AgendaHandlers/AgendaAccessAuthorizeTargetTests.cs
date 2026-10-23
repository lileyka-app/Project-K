using FluentAssertions;
using Moq;
using ProjectK.BusinessLogic.Modules.AuthModule.Services;
using ProjectK.BusinessLogic.Modules.KurinModule.Services;
using ProjectK.BusinessLogic.Tests.TestHelpers;
using ProjectK.Common.Interfaces;
using ProjectK.Common.Interfaces.Modules.InfrastructureModule;
using ProjectK.Common.Interfaces.Modules.MemberModule;
using ProjectK.Common.Models.Authorization;
using ProjectK.Common.Models.Authorization.KurinPolicy;
using ProjectK.Common.Models.Dtos.KurinModule;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Records;
using Xunit;

namespace ProjectK.BusinessLogic.Tests.KurinModule.HandlerTests.AgendaHandlers;

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
    private readonly Mock<IResourceScopeReader> _scopes = new();

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
        _scopes.Setup(x => x.GetLedGroupKeysAsync(_userKey, _kurinKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { _ledGroup });
    }

    private void Scope(ResourceType type, Guid key, ResourceScope scope) =>
        _scopes.Setup(x => x.GetScopeAsync(type, key, It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(scope);

    private AgendaAccess AccessFor(string role)
    {
        var roles = role == SystemRole.Member ? new[] { SystemRole.Member } : new[] { role, SystemRole.Member };
        var user = new Mock<ICurrentUserContext>();
        user.SetupGet(x => x.IsAuthenticated).Returns(true);
        user.SetupGet(x => x.UserId).Returns(_userKey);
        user.SetupGet(x => x.KurinKey).Returns(_kurinKey);
        user.SetupGet(x => x.Roles).Returns(roles);
        user.Setup(x => x.IsInRole(It.IsAny<string>())).Returns((string r) => roles.Contains(r, StringComparer.OrdinalIgnoreCase));

        var permissions = new CurrentPermissions(user.Object, _policies);
        var resourceAccess = new ResourceAccessService(_scopes.Object, user.Object, new PassThroughBackendCache(), permissions);
        return new AgendaAccess(user.Object, Mock.Of<IUnitOfWork>(), Mock.Of<IMemberDirectory>(), _scopes.Object, resourceAccess, permissions);
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

    private static readonly string Zvyazkovyi = SystemRole.ForOffice(LeadershipType.KV, LeadershipRole.Zvyazkovyi);
    private static readonly string Vykhovnyk = SystemRole.ForOffice(LeadershipType.KV, LeadershipRole.Vykhovnyk);
    private static readonly string Hurtkovyy = SystemRole.ForOffice(LeadershipType.Group, LeadershipRole.Hurtkoviy);

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
        var access = AccessFor(SystemRole.ForOffice(LeadershipType.Kurin, LeadershipRole.Kurinnuy));

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
