using ProjectK.Common.Entities.AgendaModule;
using ProjectK.Common.Enums.AgendaModule;
using ProjectK.Common.Records.AgendaModule;

namespace ProjectK.Common.Rules.AgendaModule;

/// <summary>
/// Who moves which part of a task, and what the parts add up to. Three things that used to be one:
/// who sees a task (its audience — <c>AgendaVisibilityRules</c>), who is on the hook for it, and the state
/// it is in for each of them. Pure functions over a loaded item, so the board, the dialog and the
/// status commands cannot disagree.
/// </summary>
public static class AgendaCompletionRules
{
    /// <summary>Whether the viewer is one of the people the target stands for.</summary>
    public static bool IsIn(AgendaAssignment assignment, AgendaViewerContext viewer) => assignment.TargetType switch
    {
        AgendaTargetType.Member => viewer.ViewerMemberKey == assignment.TargetKey,
        AgendaTargetType.Group => viewer.ViewerOwnGroupKey == assignment.TargetKey,
        AgendaTargetType.Leadership => viewer.ViewerLeadershipKeys.Contains(assignment.TargetKey),
        AgendaTargetType.Tag => viewer.ViewerTagKeys.Contains(assignment.TargetKey),
        AgendaTargetType.Kurin => viewer.ViewerMemberKey.HasValue,
        _ => false
    };

    /// <summary>
    /// Whether the viewer answers for the target by office: the person of a member target, the провід
    /// or впорядник of a гурток, a holder of the провід itself. The kurin as a target is answered for
    /// only by those who see the whole kurin.
    /// </summary>
    private static bool RunsByOffice(AgendaAssignment assignment, AgendaViewerContext viewer) => assignment.TargetType switch
    {
        AgendaTargetType.Member => viewer.ViewerMemberKey == assignment.TargetKey,
        AgendaTargetType.Group => viewer.LedGroupKeys.Contains(assignment.TargetKey),
        AgendaTargetType.Leadership => viewer.ViewerLeadershipKeys.Contains(assignment.TargetKey),
        _ => false
    };

    /// <summary>The author and whoever sees the whole kurin may move any part of the task.</summary>
    private static bool Oversees(AgendaItem item, AgendaViewerContext viewer) =>
        viewer.CanSeeWholeKurin || (viewer.ViewerUserKey.HasValue && item.CreatedByUserKey == viewer.ViewerUserKey.Value);

    public static bool Runs(AgendaItem item, AgendaAssignment assignment, AgendaViewerContext viewer) =>
        Oversees(item, viewer) || RunsByOffice(assignment, viewer);

    public static AgendaCompletionMode ModeOf(AgendaAssignment assignment) =>
        assignment.TargetType == AgendaTargetType.Member ? AgendaCompletionMode.Shared : assignment.CompletionMode;

    /// <summary>Whether the viewer may move the target's single state; never in «кожному окремо», where it is derived.</summary>
    public static bool CanMoveTarget(AgendaItem item, AgendaAssignment assignment, AgendaViewerContext viewer) => ModeOf(assignment) switch
    {
        AgendaCompletionMode.Shared => Runs(item, assignment, viewer),
        AgendaCompletionMode.SharedByAnyone => Runs(item, assignment, viewer) || IsIn(assignment, viewer),
        _ => false
    };

    /// <summary>In «кожному окремо»: the person moves their own part, those who run the target anyone's.</summary>
    public static bool CanMovePart(AgendaItem item, AgendaAssignment assignment, AgendaViewerContext viewer, Guid memberKey) =>
        ModeOf(assignment) == AgendaCompletionMode.PerMember
        && (Runs(item, assignment, viewer) || viewer.ViewerMemberKey == memberKey);

    /// <summary>
    /// The viewer's own part, in the order it is meant: a task handed to them by name; then a target
    /// done each on their own that they run by office — as a whole, since the гуртковий answers for
    /// all eight, not for his own вкладка; then their row in one they only belong to; then a shared
    /// target anyone in it may close; then one they run by office. Null when the task is only theirs
    /// to follow.
    /// </summary>
    public static AgendaStake? StakeOf(AgendaItem item, AgendaViewerContext viewer)
    {
        var assignments = item.Assignments;

        var named = assignments.FirstOrDefault(a => a.TargetType == AgendaTargetType.Member && IsIn(a, viewer));
        if (named is not null)
        {
            return new AgendaStake(named, null);
        }

        var runsEach = assignments.FirstOrDefault(a => ModeOf(a) == AgendaCompletionMode.PerMember && RunsByOffice(a, viewer));
        if (runsEach is not null)
        {
            return new AgendaStake(runsEach, null);
        }

        var ownPart = assignments.FirstOrDefault(a => ModeOf(a) == AgendaCompletionMode.PerMember && IsIn(a, viewer));
        if (ownPart is not null)
        {
            return new AgendaStake(ownPart, viewer.ViewerMemberKey);
        }

        var anyone = assignments.FirstOrDefault(a => ModeOf(a) == AgendaCompletionMode.SharedByAnyone && IsIn(a, viewer));
        if (anyone is not null)
        {
            return new AgendaStake(anyone, null);
        }

        var run = assignments.FirstOrDefault(a => ModeOf(a) != AgendaCompletionMode.PerMember && RunsByOffice(a, viewer));
        return run is null ? null : new AgendaStake(run, null);
    }

    public static AgendaItemStatus StatusOf(AgendaStake stake) => stake.MemberKey is { } memberKey
        ? stake.Assignment.Progress.FirstOrDefault(p => p.MemberKey == memberKey)?.Status ?? AgendaItemStatus.Todo
        : stake.Assignment.Status;

    /// <summary>The state a board column shows to this viewer: their own part's, or the task's as a whole.</summary>
    public static AgendaItemStatus ViewerStatus(AgendaItem item, AgendaViewerContext viewer, AgendaRoster roster)
    {
        var stake = StakeOf(item, viewer);
        return stake is null ? ItemStatus(item, roster) : StatusOf(stake);
    }

    public static bool CanMoveStake(AgendaItem item, AgendaStake stake, AgendaViewerContext viewer) => stake.MemberKey is { } memberKey
        ? CanMovePart(item, stake.Assignment, viewer, memberKey)
        : CanMoveTarget(item, stake.Assignment, viewer);

    /// <summary>
    /// What a drag on the board moves. The viewer's own part when they have one; otherwise every
    /// shared target they may move — the author closing the task they set. Targets done «кожному
    /// окремо» are never moved wholesale: marking eight people done at once is not something a drag
    /// should do, so those are moved one person at a time from the dialog.
    /// </summary>
    public static IReadOnlyList<AgendaStake> BoardMove(AgendaItem item, AgendaViewerContext viewer)
    {
        var stake = StakeOf(item, viewer);
        if (stake is not null)
        {
            return CanMoveStake(item, stake, viewer) ? [stake] : [];
        }

        return item.Assignments
            .Where(a => ModeOf(a) != AgendaCompletionMode.PerMember && CanMoveTarget(item, a, viewer))
            .Select(a => new AgendaStake(a, null))
            .ToList();
    }

    /// <summary>
    /// Whether a drag on the board moves anything for this viewer: their own part, or the shared
    /// targets they answer for. Seeing a task is not enough — a youth whose гурток was handed a task
    /// to be closed by its провід sees it without being able to close it.
    /// </summary>
    public static bool CanChangeStatus(AgendaItem item, AgendaViewerContext viewer) => BoardMove(item, viewer).Count > 0;

    /// <summary>
    /// A target done «кожному окремо», over the people in it now: all done → done, anyone started →
    /// in progress. Rows of people who have left are history and do not count.
    /// </summary>
    public static AgendaItemStatus TargetStatus(AgendaAssignment assignment, AgendaRoster roster)
    {
        if (ModeOf(assignment) != AgendaCompletionMode.PerMember)
        {
            return assignment.Status;
        }

        var people = roster.PeopleIn(assignment);
        if (people.Count == 0)
        {
            return AgendaItemStatus.Todo;
        }

        var parts = PartsByMember(assignment).Where(p => people.Contains(p.Key)).ToDictionary(p => p.Key, p => p.Value.Status);
        if (people.All(person => parts.TryGetValue(person, out var status) && status == AgendaItemStatus.Done))
        {
            return AgendaItemStatus.Done;
        }

        return parts.Values.Any(status => status != AgendaItemStatus.Todo) ? AgendaItemStatus.InProgress : AgendaItemStatus.Todo;
    }

    /// <summary>The task as a whole: done once every target is, started once any is. An item without targets keeps its own state.</summary>
    public static AgendaItemStatus ItemStatus(AgendaItem item, AgendaRoster roster)
    {
        if (item.Assignments.Count == 0)
        {
            return item.Status;
        }

        var states = item.Assignments.Select(a => TargetStatus(a, roster)).ToList();
        if (states.All(s => s == AgendaItemStatus.Done))
        {
            return AgendaItemStatus.Done;
        }

        return states.Any(s => s != AgendaItemStatus.Todo) ? AgendaItemStatus.InProgress : AgendaItemStatus.Todo;
    }

    /// <summary>Each person's part, one per person — the unique index guarantees it in the database.</summary>
    public static IReadOnlyDictionary<Guid, AgendaAssignmentProgress> PartsByMember(AgendaAssignment assignment) =>
        assignment.Progress.GroupBy(p => p.MemberKey).ToDictionary(g => g.Key, g => g.Last());

    /// <summary>
    /// Sets the task's own state and keeps <see cref="AgendaItem.CompletedAtUtc"/> with it: stamped when
    /// it becomes done, cleared when it is reopened — so auto-archiving counts from the last close.
    /// </summary>
    public static void SetItemStatus(AgendaItem item, AgendaItemStatus status, DateTime nowUtc)
    {
        if (status == AgendaItemStatus.Done)
        {
            if (item.Status != AgendaItemStatus.Done || item.CompletedAtUtc is null)
            {
                item.CompletedAtUtc = nowUtc;
            }
        }
        else
        {
            item.CompletedAtUtc = null;
        }

        item.Status = status;
    }

    /// <summary>Whether anything on the target has moved — after that its mode is fixed.</summary>
    public static bool HasMoved(AgendaAssignment assignment) =>
        assignment.Status != AgendaItemStatus.Todo || assignment.StatusChangedAtUtc.HasValue || assignment.Progress.Count > 0;
}
