using ProjectK.BusinessLogic.Services.Caching;
using ProjectK.Common.Contracts.AuthModule;
using ProjectK.Common.Contracts.InfrastructureModule;
using ProjectK.Common.Contracts.Shared;
using ProjectK.Common.Enums.AuthModule;
using ProjectK.Common.Enums.MemberModule;
using ProjectK.Common.Records.MemberModule;
using ProjectK.Common.Rules.MemberModule;

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
    private readonly IResourceScopeRepository _scopeReader;
    private readonly IBackendCache _cache;

    private MemberFieldMask? _mask;

    public MemberFieldAccess(
        ICurrentUserContext currentUser,
        ICurrentPermissions permissions,
        IUnitOfWork unitOfWork,
        IBackendCache cache)
    {
        _currentUser = currentUser;
        _permissions = permissions;
        _scopeReader = unitOfWork.ResourceScopes;
        _cache = cache;
    }

    public async Task<MemberFieldMask> ForViewerAsync(CancellationToken cancellationToken = default)
    {
        if (_mask is not null)
        {
            return _mask;
        }

        var reach = new Dictionary<MemberDataGroup, CapabilityReach>();
        foreach (var info in MemberDataFieldRules.All)
        {
            reach[info.Group] = await _permissions.ReachAsync(info.Capability, cancellationToken);
        }

        IReadOnlyCollection<Guid> ownGroups = [];
        if (reach.ContainsValue(CapabilityReach.OwnGroup)
            && _currentUser.UserId is { } userKey
            && _currentUser.KurinKey is { } kurinKey)
        {
            ownGroups = await _cache.GetOrCreateAsync(
                BackendCacheRules.MentorScopeReads,
                $"ledgroups:kurin:{kurinKey}",
                token => _scopeReader.GetLedGroupKeysAsync(userKey, kurinKey, token),
                cancellationToken,
                CacheScopeContext.From(_currentUser));
        }

        _mask = new MemberFieldMask(_currentUser.UserId, reach, ownGroups);
        return _mask;
    }
}
