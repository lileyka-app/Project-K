using ProjectK.BusinessLogic.Modules.ArchiveModule.Models;
using ProjectK.Common.Contracts.IdentityModule;
using ProjectK.Common.Contracts.InfrastructureModule;
using ProjectK.Common.Contracts.MemberModule;
using ProjectK.Common.Contracts.Shared;
using ProjectK.Common.Enums.IdentityModule;

namespace ProjectK.BusinessLogic.Modules.ArchiveModule.Services;

/// <summary>
/// Resolves <see cref="ArchiveReachResult"/>. The archive is read in the kurin the session acts in
/// and nowhere else, like the journal; a group-scoped grant with no гурток led reaches nothing.
/// </summary>
public sealed class ArchiveReach
{
    private readonly ICurrentUserContext _currentUser;
    private readonly ICurrentPermissions _permissions;
    private readonly IResourceScopeReader _scopeReader;
    private readonly ICurrentMember _currentMember;

    public ArchiveReach(
        ICurrentUserContext currentUser,
        ICurrentPermissions permissions,
        IUnitOfWork unitOfWork,
        ICurrentMember currentMember,
        IResourceScopeReader scopeReader)
    {
        _currentUser = currentUser;
        _permissions = permissions;
        _scopeReader = scopeReader;
        _currentMember = currentMember;
    }

    public async Task<ArchiveReachResult?> ResolveAsync(Guid kurinKey, ResourceAction action, CancellationToken cancellationToken)
    {
        if (_currentUser.KurinKey != kurinKey || _currentUser.UserId is not { } userKey)
        {
            return null;
        }

        var scope = await _permissions.WidestScopeAsync(ResourceType.KurinArchive, action, cancellationToken);
        if (scope is not (AccessScope.KurinWide or AccessScope.OwnGroups))
        {
            return null;
        }

        var self = (await _currentMember.FindAsync(cancellationToken))?.MemberKey;
        if (scope == AccessScope.KurinWide)
        {
            return new ArchiveReachResult(true, new HashSet<Guid>(), self);
        }

        var led = await _scopeReader.GetLedGroupKeysAsync(userKey, kurinKey, cancellationToken);
        return led.Count == 0 ? null : new ArchiveReachResult(false, led.ToHashSet(), self);
    }
}
