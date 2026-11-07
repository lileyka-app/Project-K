using ProjectK.Common.Interfaces.Modules.AuthModule;
using ProjectK.Common.Interfaces.Modules.InfrastructureModule;
using ProjectK.Common.Interfaces.Modules.MemberModule;
using ProjectK.Common.Models.Authorization;
using ProjectK.Common.Models.Enums;

namespace ProjectK.BusinessLogic.Modules.KurinModule.Services;

/// <summary>
/// How far one caller reaches into the kurin's archive for one action: the whole of it, or the
/// records of the гуртки they lead. The person behind the account comes with it, because nobody
/// corrects or deletes their own record — the rule probes and badges already follow.
/// </summary>
public sealed record ArchiveReachResult(bool KurinWide, IReadOnlySet<Guid> LedGroups, Guid? OwnMemberKey)
{
    /// <summary>Whether a record that touches these гуртки (none for a kurin-level one) is within reach.</summary>
    public bool Reaches(IEnumerable<Guid?> groupKeys) =>
        KurinWide || groupKeys.Any(groupKey => groupKey is { } key && LedGroups.Contains(key));

    public bool IsOwn(Guid memberKey) => OwnMemberKey == memberKey;
}

/// <summary>
/// Resolves <see cref="ArchiveReachResult"/>. The archive is read in the kurin the session acts in
/// and nowhere else, like the journal; a group-scoped grant with no гурток led reaches nothing.
/// </summary>
public sealed class ArchiveReach
{
    private readonly ICurrentUserContext _currentUser;
    private readonly ICurrentPermissions _permissions;
    private readonly IResourceScopeReader _scopeReader;
    private readonly IMemberDirectory _members;

    public ArchiveReach(
        ICurrentUserContext currentUser,
        ICurrentPermissions permissions,
        IResourceScopeReader scopeReader,
        IMemberDirectory members)
    {
        _currentUser = currentUser;
        _permissions = permissions;
        _scopeReader = scopeReader;
        _members = members;
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

        var self = (await _members.FindByAccountAsync(userKey, cancellationToken))?.MemberKey;
        if (scope == AccessScope.KurinWide)
        {
            return new ArchiveReachResult(true, new HashSet<Guid>(), self);
        }

        var led = await _scopeReader.GetLedGroupKeysAsync(userKey, kurinKey, cancellationToken);
        return led.Count == 0 ? null : new ArchiveReachResult(false, led.ToHashSet(), self);
    }
}
