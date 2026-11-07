using ProjectK.BusinessLogic.Modules.KurinModule.Models;
using ProjectK.BusinessLogic.Services.Caching;
using ProjectK.Common.Interfaces;
using ProjectK.Common.Interfaces.Modules.AuthModule;
using ProjectK.Common.Interfaces.Modules.InfrastructureModule;
using ProjectK.Common.Models.Authorization;
using ProjectK.Common.Models.Authorization.KurinPolicy;
using ProjectK.Common.Models.Tags;

namespace ProjectK.BusinessLogic.Modules.KurinModule.Services;

/// <summary>
/// Whose tags the viewer sees: the kurin's switch first, then <see cref="KurinCapability.SeeTags"/>
/// reaching the whole kurin or the гуртки they lead, and their own tags when the kurin shows people
/// theirs. Whoever may manage tags sees them all. Every read that hands tags out goes through here.
/// </summary>
public sealed class KurinTagVisibility : IKurinTagVisibility
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserContext _currentUser;
    private readonly ICurrentPermissions _permissions;
    private readonly IResourceScopeReader _scopeReader;
    private readonly IBackendCache _cache;

    public KurinTagVisibility(
        IUnitOfWork unitOfWork,
        ICurrentUserContext currentUser,
        ICurrentPermissions permissions,
        IResourceScopeReader scopeReader,
        IBackendCache cache)
    {
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
        _permissions = permissions;
        _scopeReader = scopeReader;
        _cache = cache;
    }

    /// <summary>The kurin's switches with its defaults filled in; null when there is no such kurin.</summary>
    public async Task<KurinTagSettingsState?> SettingsAsync(Guid kurinKey, CancellationToken cancellationToken = default)
    {
        var kurin = await _unitOfWork.Kurins.GetByKeyAsync(kurinKey, cancellationToken);
        if (kurin is null)
        {
            return null;
        }

        var stored = await _unitOfWork.KurinTags.GetSettingsAsync(kurinKey, cancellationToken);
        var byDefault = KurinTagRules.EnabledByDefault(kurin.Branch);
        return new KurinTagSettingsState(stored?.Enabled ?? byDefault, byDefault, stored?.OwnTagsVisible ?? true);
    }

    /// <summary>
    /// Whether the viewer may define and put on tags in <paramref name="kurinKey"/>: the kurin they act
    /// in, or any kurin for an admin outside all of them.
    /// </summary>
    public async Task<bool> CanManageAsync(Guid kurinKey, CancellationToken cancellationToken = default)
    {
        if (IsUnscopedAdmin())
        {
            return true;
        }

        return _currentUser.KurinKey == kurinKey
            && await _permissions.ReachAsync(KurinCapability.ManageTags, cancellationToken) == CapabilityReach.Kurin;
    }

    /// <summary>
    /// Fills <see cref="MemberResponse.TagKeys"/> of people seen in <paramref name="kurinKey"/> with
    /// the tags the viewer may see, in one read. Leaves them empty in a kurin with tags off. An empty
    /// <paramref name="kurinKey"/> is the kurin the viewer acts in — a гурток's list knows no other.
    /// </summary>
    public async Task FillAsync(Guid kurinKey, IReadOnlyCollection<MemberResponse> rows, CancellationToken cancellationToken = default)
    {
        foreach (var row in rows)
        {
            row.TagKeys = [];
        }

        if (kurinKey == Guid.Empty)
        {
            kurinKey = _currentUser.KurinKey ?? Guid.Empty;
        }

        if (rows.Count == 0 || kurinKey == Guid.Empty)
        {
            return;
        }

        var settings = await SettingsAsync(kurinKey, cancellationToken);
        if (settings is not { Enabled: true })
        {
            return;
        }

        var shows = await ViewerAsync(kurinKey, settings.OwnTagsVisible, cancellationToken);
        var visibleRows = rows.Where(row => shows(row.UserKey, row.GroupKey == Guid.Empty ? null : row.GroupKey)).ToList();
        if (visibleRows.Count == 0)
        {
            return;
        }

        var assignments = await _unitOfWork.KurinTags.GetAssignmentsAsync(
            kurinKey,
            visibleRows.Select(row => row.MemberKey).Distinct().ToList(),
            cancellationToken);
        var byMember = assignments.ToLookup(a => a.MemberKey, a => a.KurinTagKey);
        foreach (var row in visibleRows)
        {
            row.TagKeys = [.. byMember[row.MemberKey]];
        }
    }

    public async Task<Func<Guid?, Guid?, bool>> ShowsTagsOfAsync(Guid kurinKey, CancellationToken cancellationToken = default)
    {
        var settings = await SettingsAsync(kurinKey, cancellationToken);
        return settings is { Enabled: true }
            ? await ViewerAsync(kurinKey, settings.OwnTagsVisible, cancellationToken)
            : (_, _) => false;
    }

    public async Task<CapabilityReach> CarrierReachAsync(Guid kurinKey, CancellationToken cancellationToken = default)
    {
        if ((await SettingsAsync(kurinKey, cancellationToken))?.Enabled != true)
        {
            return CapabilityReach.None;
        }

        if (await CanManageAsync(kurinKey, cancellationToken))
        {
            return CapabilityReach.Kurin;
        }

        return _currentUser.KurinKey == kurinKey
            ? await _permissions.ReachAsync(KurinCapability.SeeTags, cancellationToken)
            : CapabilityReach.None;
    }

    // Who the viewer sees tags on, as a test of (account, гурток) of the person.
    private async Task<Func<Guid?, Guid?, bool>> ViewerAsync(Guid kurinKey, bool ownVisible, CancellationToken cancellationToken)
    {
        if (await CanManageAsync(kurinKey, cancellationToken))
        {
            return (_, _) => true;
        }

        var viewer = _currentUser.UserId;
        bool IsSelf(Guid? userKey) => ownVisible && userKey.HasValue && userKey == viewer;

        if (_currentUser.KurinKey != kurinKey || viewer is null)
        {
            return (userKey, _) => IsSelf(userKey);
        }

        switch (await _permissions.ReachAsync(KurinCapability.SeeTags, cancellationToken))
        {
            case CapabilityReach.Kurin:
                return (_, _) => true;

            case CapabilityReach.OwnGroup:
                var ledGroups = await _cache.GetOrCreateAsync(
                    BackendCachePolicies.MentorScopeReads,
                    $"ledgroups:kurin:{kurinKey}",
                    token => _scopeReader.GetLedGroupKeysAsync(viewer.Value, kurinKey, token),
                    cancellationToken,
                    CacheScopeContext.From(_currentUser));
                return (userKey, groupKey) => IsSelf(userKey) || (groupKey.HasValue && ledGroups.Contains(groupKey.Value));

            default:
                return (userKey, _) => IsSelf(userKey);
        }
    }

    private bool IsUnscopedAdmin() => _currentUser.IsInRole(SystemRole.Admin) && _currentUser.KurinKey is null;
}

/// <summary>A kurin's tag switches with the defaults filled in.</summary>
public sealed record KurinTagSettingsState(bool Enabled, bool EnabledByDefault, bool OwnTagsVisible);
