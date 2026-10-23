using Moq;
using ProjectK.BusinessLogic.Modules.AuthModule.Services;
using ProjectK.BusinessLogic.Services.Caching;
using ProjectK.Common.Interfaces.Modules.AuthModule;
using ProjectK.Common.Interfaces.Modules.InfrastructureModule;
using ProjectK.Common.Models.Authorization;
using ProjectK.Common.Models.Authorization.KurinPolicy;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Records;

namespace ProjectK.BusinessLogic.Tests.TestHelpers;

/// <summary>
/// The real access decision — roles folded into audiences, the kurin's policy, the member's гурток —
/// with only the reads faked: one kurin, one member in one гурток, a policy the test may change.
/// </summary>
internal sealed class PolicyAccess
{
    public Guid KurinKey { get; } = Guid.NewGuid();
    public Guid GroupKey { get; } = Guid.NewGuid();
    public Guid OtherGroupKey { get; } = Guid.NewGuid();
    public Guid MemberKey { get; } = Guid.NewGuid();
    public Guid MemberUserKey { get; } = Guid.NewGuid();
    public Guid ActorUserKey { get; } = Guid.NewGuid();

    public FixedKurinAccessPolicyProvider Policies { get; } = new();

    /// <summary>The гуртки the actor leads; the member's own by default.</summary>
    public List<Guid> LedGroups { get; } = [];

    public PolicyAccess()
    {
        LedGroups.Add(GroupKey);
    }

    public ICurrentUserContext Actor(params string[] roles) => User(ActorUserKey, roles);

    public ICurrentUserContext User(Guid userKey, params string[] roles)
    {
        var held = roles.Append(SystemRole.Member).ToArray();
        var user = new Mock<ICurrentUserContext>();
        user.SetupGet(x => x.IsAuthenticated).Returns(true);
        user.SetupGet(x => x.UserId).Returns(userKey);
        user.SetupGet(x => x.KurinKey).Returns(KurinKey);
        user.SetupGet(x => x.Roles).Returns(held);
        user.Setup(x => x.IsInRole(It.IsAny<string>()))
            .Returns((string role) => held.Contains(role, StringComparer.OrdinalIgnoreCase));
        return user.Object;
    }

    public IResourceAccessService ServiceFor(ICurrentUserContext user)
    {
        var scopes = new Mock<IResourceScopeReader>();
        scopes.Setup(x => x.GetScopeAsync(It.IsAny<ResourceType>(), MemberKey, It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ResourceScope(KurinKey, GroupKey, MemberUserKey));
        scopes.Setup(x => x.GetLedGroupKeysAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => LedGroups.ToArray());

        return new ResourceAccessService(scopes.Object, user, new PassThroughBackendCache(), new CurrentPermissions(user, Policies));
    }

    public ICurrentPermissions PermissionsFor(ICurrentUserContext user) => new CurrentPermissions(user, Policies);

    public static string Office(LeadershipType type, LeadershipRole role) => SystemRole.ForOffice(type, role);

    public static readonly string Zvyazkovyi = Office(LeadershipType.KV, LeadershipRole.Zvyazkovyi);
    public static readonly string Vykhovnyk = Office(LeadershipType.KV, LeadershipRole.Vykhovnyk);
    public static readonly string Hurtkovyy = Office(LeadershipType.Group, LeadershipRole.Hurtkoviy);

    /// <summary>A policy that sets the given cells and leaves the rest at their defaults.</summary>
    public static KurinAccessPolicy With(params (KurinCapability Capability, PolicyAudience Audience, CapabilityReach Reach)[] cells)
    {
        var matrix = cells
            .GroupBy(cell => cell.Capability)
            .ToDictionary(
                row => row.Key.ToString(),
                row => (IReadOnlyDictionary<string, string?>)row.ToDictionary(cell => cell.Audience.ToString(), cell => (string?)cell.Reach.ToString()));
        return KurinAccessPolicy.FromMatrix(matrix);
    }
}

/// <summary>A policy provider holding whatever policy the test last set.</summary>
internal sealed class FixedKurinAccessPolicyProvider : IKurinAccessPolicyProvider
{
    public KurinAccessPolicy Policy { get; set; } = KurinAccessPolicy.Default;

    public Task<KurinAccessPolicy> GetAsync(Guid kurinKey, CancellationToken cancellationToken = default) => Task.FromResult(Policy);

    public void Invalidate()
    {
    }
}

/// <summary>Runs the factory every time, so a test never sees a stale read.</summary>
internal sealed class PassThroughBackendCache : IBackendCache
{
    public Task<T> GetOrCreateAsync<T>(
        CachePolicy policy,
        string key,
        Func<CancellationToken, Task<T>> factory,
        CancellationToken cancellationToken,
        CacheScopeContext? scopeContext = null) => factory(cancellationToken);

    public void Invalidate(CachePolicy policy)
    {
    }

    public void InvalidateByPrefix(string prefix)
    {
    }
}
