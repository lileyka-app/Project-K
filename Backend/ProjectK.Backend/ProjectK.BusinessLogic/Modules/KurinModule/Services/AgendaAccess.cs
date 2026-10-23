using ProjectK.Common.Entities.KurinModule.Agenda;
using ProjectK.Common.Extensions;
using ProjectK.Common.Interfaces;
using ProjectK.Common.Interfaces.Modules.InfrastructureModule;
using ProjectK.Common.Interfaces.Modules.MemberModule;
using ProjectK.Common.Models.Authorization;
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

public interface IAgendaAccess
{
    /// <summary>Resolves the current user's agenda context inside <paramref name="kurinKey"/>.</summary>
    Task<AgendaViewerContext> BuildViewerAsync(Guid kurinKey, CancellationToken cancellationToken = default);

    /// <summary>
    /// Whether the current user may aim an item at <paramref name="target"/>. Reuses the tested resource
    /// guard, so a mentor/group leader is bounded to assigned groups and their members while a
    /// manager/admin reaches the whole kurin.
    /// </summary>
    Task<ResourceAccessDecision> AuthorizeTargetAsync(
        AgendaTargetInput target,
        ResourceAction action,
        CancellationToken cancellationToken = default);
}

public sealed class AgendaAccess : IAgendaAccess
{
    private readonly ICurrentUserContext _currentUser;
    private readonly IUnitOfWork _uow;
    private readonly IMemberDirectory _members;
    private readonly IResourceScopeReader _scopeReader;
    private readonly IResourceAccessService _resourceAccess;

    public AgendaAccess(
        ICurrentUserContext currentUser,
        IUnitOfWork uow,
        IMemberDirectory members,
        IResourceScopeReader scopeReader,
        IResourceAccessService resourceAccess)
    {
        _currentUser = currentUser;
        _uow = uow;
        _members = members;
        _scopeReader = scopeReader;
        _resourceAccess = resourceAccess;
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

    public async Task<ResourceAccessDecision> AuthorizeTargetAsync(
        AgendaTargetInput target,
        ResourceAction action,
        CancellationToken cancellationToken = default)
    {
        // Aiming at a провід is authorized like aiming at its scope: a гуртковий провід as its group,
        // a курінний провід / КВ as the whole kurin. This reuses the tested resource guard.
        if (target.TargetType == AgendaTargetType.Leadership)
        {
            var leadership = await _uow.Leaderships.GetByKeyAsync(target.TargetKey, cancellationToken);
            if (leadership is null)
            {
                return ResourceAccessDecision.Deny("Leadership target was not found.");
            }

            return leadership.Type == LeadershipType.Group && leadership.GroupKey.HasValue
                ? await _resourceAccess.CheckAccessAsync(ResourceType.Group, action, leadership.GroupKey.Value, cancellationToken)
                : await _resourceAccess.CheckAccessAsync(ResourceType.Kurin, action, leadership.KurinKey ?? Guid.Empty, cancellationToken);
        }

        var resourceType = target.TargetType switch
        {
            AgendaTargetType.Kurin => ResourceType.Kurin,
            AgendaTargetType.Group => ResourceType.Group,
            AgendaTargetType.Member => ResourceType.Member,
            _ => ResourceType.Kurin
        };

        return await AuthorizeScopeAsync(resourceType, target.TargetKey, action, cancellationToken);
    }

    /// <summary>
    /// The resource guard first: whoever runs a group or the kurin may aim at it. When that says no
    /// and the action is Create, the провід rule follows: a member of a провід may address what the
    /// office covers — the whole kurin for the курінний провід and КВ, the own гурток for a гуртковий
    /// провід — without holding the far wider right to create groups or members there. Which of the
    /// two the person is comes from their planning reach, the one permission the map already scopes
    /// that way.
    /// </summary>
    private async Task<ResourceAccessDecision> AuthorizeScopeAsync(
        ResourceType resourceType,
        Guid key,
        ResourceAction action,
        CancellationToken cancellationToken)
    {
        var decision = await _resourceAccess.CheckAccessAsync(resourceType, action, key, cancellationToken);
        if (decision.IsAllowed || action != ResourceAction.Create)
        {
            return decision;
        }

        if (!_currentUser.UserId.HasValue || !_currentUser.HasPermission(ResourceType.AgendaItem, ResourceAction.Create))
        {
            return decision;
        }

        var author = await _members.FindByAccountAsync(_currentUser.UserId.Value, cancellationToken);
        if (author is null)
        {
            return decision;
        }

        var (kurinKey, groupKey) = await LocateAsync(resourceType, key, author.KurinKey, cancellationToken);
        if (kurinKey != author.KurinKey)
        {
            return decision;
        }

        var reach = RolePermissionMap.WidestScope(_currentUser.StaticPermissions(), ResourceType.PlanningSession, ResourceAction.Create);
        if (reach == AccessScope.KurinWide)
        {
            return ResourceAccessDecision.Allow("Курінний провід addresses the whole kurin.");
        }

        if (reach == AccessScope.OwnGroups && groupKey.HasValue && groupKey == author.GroupKey)
        {
            return ResourceAccessDecision.Allow("Гуртковий провід addresses its own гурток.");
        }

        return decision;
    }

    /// <summary>Where a target lives: its kurin, and its гурток when it has one.</summary>
    private async Task<(Guid? KurinKey, Guid? GroupKey)> LocateAsync(
        ResourceType resourceType,
        Guid key,
        Guid authorKurinKey,
        CancellationToken cancellationToken)
    {
        switch (resourceType)
        {
            case ResourceType.Kurin:
                return (key, null);
            case ResourceType.Group:
                var groups = await _uow.Groups.GetAllAsync(authorKurinKey, cancellationToken);
                return groups.Any(group => group.GroupKey == key) ? (authorKurinKey, key) : (null, null);
            case ResourceType.Member:
                var member = await _members.FindAsync(key, cancellationToken);
                return member is null ? (null, null) : (member.KurinKey, member.GroupKey);
            default:
                return (null, null);
        }
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
