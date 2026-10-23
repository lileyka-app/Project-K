using ProjectK.Common.Entities.KurinModule.Agenda;
using ProjectK.Common.Extensions;
using ProjectK.Common.Interfaces;
using ProjectK.Common.Interfaces.Modules.AuthModule;
using ProjectK.Common.Interfaces.Modules.InfrastructureModule;
using ProjectK.Common.Interfaces.Modules.MemberModule;
using ProjectK.Common.Models.Authorization;
using ProjectK.Common.Models.Authorization.KurinPolicy;
using ProjectK.Common.Models.Dtos;
using ProjectK.Common.Models.Dtos.KurinModule;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Records;

namespace ProjectK.BusinessLogic.Modules.KurinModule.Services;

/// <summary>
/// Who the current user is on the agenda: their linked member/group, the groups whose items they may
/// see, and whether they manage the whole kurin. Built once per request from the auth claims and the
/// member record.
/// </summary>
public sealed record AgendaViewerContext(
    Guid KurinKey,
    Guid? ViewerUserKey,
    Guid? ViewerMemberKey,
    Guid? ViewerOwnGroupKey,
    IReadOnlyCollection<Guid> VisibilityGroupKeys,
    IReadOnlyCollection<Guid> ViewerLeadershipKeys,
    bool CanSeeWholeKurin,
    bool IsLeadership,
    IReadOnlyCollection<Guid>? LedGroupKeysOrNull = null)
{
    /// <summary>The гуртки the viewer runs as провід or впорядник — narrower than what they see, which also holds their own гурток.</summary>
    public IReadOnlyCollection<Guid> LedGroupKeys => LedGroupKeysOrNull ?? Array.Empty<Guid>();

    public AgendaViewerScope ToScope() =>
        new(KurinKey, ViewerUserKey, ViewerMemberKey, VisibilityGroupKeys, ViewerLeadershipKeys, CanSeeWholeKurin);
}

/// <summary>
/// How far the current user may assign agenda items in one kurin, as the kurin's policy says: one
/// reach per kind of target, and the гуртки an own-group reach covers. Answers the same question as
/// <see cref="IAgendaAccess.AuthorizeTargetAsync"/> for many targets at once, without a read per target.
/// </summary>
/// <param name="Kurin">The reach of <see cref="KurinCapability.AssignKurin"/>.</param>
/// <param name="Groups">The reach of <see cref="KurinCapability.AssignGroups"/>.</param>
/// <param name="Members">The reach of <see cref="KurinCapability.AssignMembers"/>.</param>
/// <param name="Provid">The reach of <see cref="KurinCapability.AssignProvid"/>.</param>
/// <param name="LedGroupKeys">The гуртки the user leads, their own member гурток included; empty unless some reach is own-group.</param>
/// <param name="UserKey">The user's account, which an own-group reach always covers.</param>
public sealed record AgendaAssignReach(
    CapabilityReach Kurin,
    CapabilityReach Groups,
    CapabilityReach Members,
    CapabilityReach Provid,
    IReadOnlySet<Guid> LedGroupKeys,
    Guid? UserKey)
{
    /// <summary>Whether the user may author an item at all: some kind of target is within reach.</summary>
    public bool CanAuthor => Kurin > CapabilityReach.None || Groups > CapabilityReach.None
        || Members > CapabilityReach.None || Provid > CapabilityReach.None;

    /// <summary>Whether the whole kurin may be a target.</summary>
    public bool ReachesKurin => Kurin == CapabilityReach.Kurin;

    /// <summary>Whether <paramref name="groupKey"/> may be a target.</summary>
    public bool ReachesGroup(Guid groupKey) => Covers(Groups, groupKey, null);

    /// <summary>Whether a person seated in <paramref name="groupKey"/> with <paramref name="memberUserKey"/> may be a target.</summary>
    public bool ReachesMember(Guid? groupKey, Guid? memberUserKey) => Covers(Members, groupKey, memberUserKey);

    /// <summary>Whether the провід of <paramref name="groupKey"/> may be a target; null for the КВ and the курінний провід.</summary>
    public bool ReachesProvid(Guid? groupKey) => Covers(Provid, groupKey, null);

    // The same rule ResourceAccessService applies to an own-group grant: one's own record, or a
    // resource in a гурток one leads.
    private bool Covers(CapabilityReach reach, Guid? groupKey, Guid? memberUserKey) => reach switch
    {
        CapabilityReach.Kurin => true,
        CapabilityReach.OwnGroup => (memberUserKey.HasValue && memberUserKey == UserKey)
            || (groupKey.HasValue && LedGroupKeys.Contains(groupKey.Value)),
        _ => false
    };
}

public interface IAgendaAccess
{
    /// <summary>Resolves the current user's agenda context inside <paramref name="kurinKey"/>.</summary>
    Task<AgendaViewerContext> BuildViewerAsync(Guid kurinKey, CancellationToken cancellationToken = default);

    /// <summary>
    /// Whether the current user may assign an item to <paramref name="target"/>: the kurin's policy
    /// row for that kind of target (<see cref="KurinCapability.AssignKurin"/>, <c>AssignGroups</c>,
    /// <c>AssignMembers</c>, <c>AssignProvid</c>), checked against the target's scope by the resource
    /// guard — which also refuses a target outside the user's kurin.
    /// </summary>
    Task<ResourceAccessDecision> AuthorizeTargetAsync(
        AgendaTargetInput target,
        CancellationToken cancellationToken = default);

    /// <summary>Whether the current user may author agenda items at all: some kind of target is within reach.</summary>
    Task<bool> CanAuthorAsync(CancellationToken cancellationToken = default);

    /// <summary>The current user's assigning reach inside <paramref name="kurinKey"/>, for deciding many targets at once.</summary>
    Task<AgendaAssignReach> GetAssignReachAsync(Guid kurinKey, CancellationToken cancellationToken = default);
}

public sealed class AgendaAccess : IAgendaAccess
{
    private static readonly KurinCapability[] AssignCapabilities =
    [
        KurinCapability.AssignKurin, KurinCapability.AssignGroups, KurinCapability.AssignMembers, KurinCapability.AssignProvid
    ];

    private readonly ICurrentUserContext _currentUser;
    private readonly IUnitOfWork _uow;
    private readonly IMemberDirectory _members;
    private readonly IResourceScopeReader _scopeReader;
    private readonly IResourceAccessService _resourceAccess;
    private readonly ICurrentPermissions _permissions;

    public AgendaAccess(
        ICurrentUserContext currentUser,
        IUnitOfWork uow,
        IMemberDirectory members,
        IResourceScopeReader scopeReader,
        IResourceAccessService resourceAccess,
        ICurrentPermissions permissions)
    {
        _currentUser = currentUser;
        _uow = uow;
        _members = members;
        _scopeReader = scopeReader;
        _resourceAccess = resourceAccess;
        _permissions = permissions;
    }

    public async Task<AgendaViewerContext> BuildViewerAsync(Guid kurinKey, CancellationToken cancellationToken = default)
    {
        var userKey = _currentUser.UserId;

        // Whole-kurin leadership can manage groups anywhere; a гуртковий leader manages only its
        // groups. Both questions are asked through the same extensions every other handler uses.
        var canSeeWholeKurin = _currentUser.CanManageWholeKurin();
        var isLeadership = _currentUser.IsLeadership();

        Guid? memberKey = null;
        Guid? ownGroupKey = null;
        if (userKey.HasValue)
        {
            var member = await _members.FindByAccountAsync(userKey.Value, cancellationToken);
            if (member is not null && member.KurinKey == kurinKey)
            {
                memberKey = member.MemberKey;
                ownGroupKey = member.GroupKey;
            }
        }

        // Проводи/КВ the viewer belongs to — so an item aimed at their провід is visible to them.
        var leadershipKeys = memberKey.HasValue
            ? await _uow.Leaderships.GetActiveLeadershipKeysForMemberAsync(memberKey.Value, cancellationToken)
            : (IReadOnlyList<Guid>)Array.Empty<Guid>();

        var visibilityGroups = new HashSet<Guid>();
        if (ownGroupKey.HasValue)
        {
            visibilityGroups.Add(ownGroupKey.Value);
        }

        // A гуртковий answers for tasks set to their гурток even where their office grants no right to
        // manage it (a youth гуртковий of УПЮ), so any group office counts here, not only leadership.
        var holdsGroupOffice = (_currentUser.Roles ?? Array.Empty<string>()).Any(SystemRole.IsGroupOffice);
        IReadOnlyCollection<Guid> ledGroups = Array.Empty<Guid>();
        if ((isLeadership || holdsGroupOffice) && !canSeeWholeKurin && userKey.HasValue)
        {
            ledGroups = await _scopeReader.GetLedGroupKeysAsync(userKey.Value, kurinKey, cancellationToken);
            foreach (var groupKey in ledGroups)
            {
                visibilityGroups.Add(groupKey);
            }
        }

        return new AgendaViewerContext(
            KurinKey: kurinKey,
            ViewerUserKey: userKey,
            ViewerMemberKey: memberKey,
            ViewerOwnGroupKey: ownGroupKey,
            VisibilityGroupKeys: visibilityGroups,
            ViewerLeadershipKeys: leadershipKeys,
            CanSeeWholeKurin: canSeeWholeKurin,
            IsLeadership: isLeadership,
            LedGroupKeysOrNull: ledGroups);
    }

    public Task<ResourceAccessDecision> AuthorizeTargetAsync(
        AgendaTargetInput target,
        CancellationToken cancellationToken = default)
    {
        (KurinCapability Capability, ResourceType Scope)? right = target.TargetType switch
        {
            AgendaTargetType.Kurin => (KurinCapability.AssignKurin, ResourceType.Kurin),
            AgendaTargetType.Group => (KurinCapability.AssignGroups, ResourceType.Group),
            AgendaTargetType.Member => (KurinCapability.AssignMembers, ResourceType.Member),
            AgendaTargetType.Leadership => (KurinCapability.AssignProvid, ResourceType.Leadership),
            _ => null
        };

        // A провід's scope is its гурток's, or the kurin alone for the КВ and the курінний провід —
        // so an own-group reach covers the провід of a гурток one leads and never a kurin-level one.
        return right is { } known
            ? _resourceAccess.CheckCapabilityAsync(known.Capability, known.Scope, target.TargetKey, cancellationToken)
            : Task.FromResult(ResourceAccessDecision.Deny($"Unknown target type {target.TargetType}."));
    }

    public async Task<bool> CanAuthorAsync(CancellationToken cancellationToken = default)
    {
        foreach (var capability in AssignCapabilities)
        {
            if (await _permissions.ReachAsync(capability, cancellationToken) > CapabilityReach.None)
            {
                return true;
            }
        }

        return false;
    }

    public async Task<AgendaAssignReach> GetAssignReachAsync(Guid kurinKey, CancellationToken cancellationToken = default)
    {
        var kurin = await _permissions.ReachAsync(KurinCapability.AssignKurin, cancellationToken);
        var groups = await _permissions.ReachAsync(KurinCapability.AssignGroups, cancellationToken);
        var members = await _permissions.ReachAsync(KurinCapability.AssignMembers, cancellationToken);
        var provid = await _permissions.ReachAsync(KurinCapability.AssignProvid, cancellationToken);

        var userKey = _currentUser.UserId;
        IReadOnlySet<Guid> ledGroups = new HashSet<Guid>();
        if (userKey.HasValue && new[] { kurin, groups, members, provid }.Contains(CapabilityReach.OwnGroup))
        {
            ledGroups = (await _scopeReader.GetLedGroupKeysAsync(userKey.Value, kurinKey, cancellationToken)).ToHashSet();
        }

        return new AgendaAssignReach(kurin, groups, members, provid, ledGroups, userKey);
    }
}

/// <summary>
/// Per-item permission checks shared by the update, delete and status handlers. Kept as pure functions
/// over an already-loaded item so no extra queries run.
/// </summary>
public static class AgendaPermissions
{
    /// <summary>Creator or leadership may edit/delete an item.</summary>
    public static bool CanManage(AgendaItem item, AgendaViewerContext viewer)
    {
        if (viewer.CanSeeWholeKurin)
        {
            return true;
        }

        if (viewer.ViewerUserKey.HasValue && item.CreatedByUserKey == viewer.ViewerUserKey.Value)
        {
            return true;
        }

        if (viewer.IsLeadership)
        {
            return item.Assignments.Any(a =>
                a.TargetType == AgendaTargetType.Group && viewer.VisibilityGroupKeys.Contains(a.TargetKey));
        }

        return false;
    }

    /// <summary>
    /// Whether the item is visible to the viewer — mirrors the repository's feed filter so an RSVP is
    /// only accepted on events the user could actually see. Requires <see cref="AgendaItem.Assignments"/>.
    /// </summary>
    public static bool IsVisibleTo(AgendaItem item, AgendaViewerContext viewer)
        => AgendaVisibility.IsVisible(item, viewer.ToScope());

    /// <summary>
    /// Whether a drag on the board moves anything for this viewer: their own part, or the shared
    /// targets they answer for. Seeing a task is not enough — a youth whose гурток was handed a task
    /// to be closed by its провід sees it without being able to close it. See <see cref="AgendaCompletion"/>.
    /// </summary>
    public static bool CanChangeStatus(AgendaItem item, AgendaViewerContext viewer)
        => AgendaCompletion.BoardMove(item, viewer).Count > 0;
}
