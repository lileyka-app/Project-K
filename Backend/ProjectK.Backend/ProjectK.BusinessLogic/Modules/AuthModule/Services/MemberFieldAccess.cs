using ProjectK.BusinessLogic.Services.Caching;
using ProjectK.Common.Interfaces.Modules.AuthModule;
using ProjectK.Common.Interfaces.Modules.InfrastructureModule;
using ProjectK.Common.Models.Authorization.KurinPolicy;
using ProjectK.Common.Models.Members;

namespace ProjectK.BusinessLogic.Modules.AuthModule.Services;

/// <summary>
/// Builds the request's mask from <see cref="ICurrentPermissions.ReachAsync"/> of each data group's
/// capability. The гуртки an own-group reach covers are the caller's led гуртки — their own
/// membership's гурток included, so for a юнак it is his own гурток — read through the same cache
/// the scope checks use.
/// </summary>
public sealed class MemberFieldAccess : IMemberFieldAccess
{
    private readonly ICurrentUserContext _currentUser;
    private readonly ICurrentPermissions _permissions;
    private readonly IResourceScopeReader _scopeReader;
    private readonly IBackendCache _cache;

    private MemberFieldMask? _mask;

    public MemberFieldAccess(
        ICurrentUserContext currentUser,
        ICurrentPermissions permissions,
        IResourceScopeReader scopeReader,
        IBackendCache cache)
    {
        _currentUser = currentUser;
        _permissions = permissions;
        _scopeReader = scopeReader;
        _cache = cache;
    }

    public async Task<MemberFieldMask> ForViewerAsync(CancellationToken cancellationToken = default)
    {
        if (_mask is not null)
        {
            return _mask;
        }

        var reach = new Dictionary<MemberDataGroup, CapabilityReach>();
        foreach (var info in MemberDataFields.All)
        {
            reach[info.Group] = await _permissions.ReachAsync(info.Capability, cancellationToken);
        }

        IReadOnlyCollection<Guid> ownGroups = [];
        if (reach.ContainsValue(CapabilityReach.OwnGroup)
            && _currentUser.UserId is { } userKey
            && _currentUser.KurinKey is { } kurinKey)
        {
            ownGroups = await _cache.GetOrCreateAsync(
                BackendCachePolicies.MentorScopeReads,
                $"ledgroups:kurin:{kurinKey}",
                token => _scopeReader.GetLedGroupKeysAsync(userKey, kurinKey, token),
                cancellationToken,
                CacheScopeContext.From(_currentUser));
        }

        _mask = new MemberFieldMask(_currentUser.UserId, reach, ownGroups);
        return _mask;
    }
}
