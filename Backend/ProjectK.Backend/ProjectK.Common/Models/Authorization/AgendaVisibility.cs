using System.Linq.Expressions;
using ProjectK.Common.Entities.KurinModule.Agenda;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Records;

namespace ProjectK.Common.Models.Authorization;

/// <summary>
/// The single definition of which agenda items a viewer may see.
/// <para>
/// The rule used to exist twice — as an EF filter in the repository's feed query and again as an
/// in-memory predicate guarding RSVP — so the list a user saw and the items they were allowed to
/// answer could drift apart. It lives in Common because both the Infrastructure query and the
/// BusinessLogic guard have to agree on it.
/// </para>
/// </summary>
public static class AgendaVisibility
{
    /// <summary>
    /// Items carrying at least one assignment that reaches the viewer. Expressed as an expression tree
    /// so EF Core can translate it into the feed query; callers holding an already-loaded item should
    /// use <see cref="IsVisible"/> instead.
    /// <para>
    /// Does not cover whole-kurin viewers — they see everything, which both call sites short-circuit
    /// before narrowing.
    /// </para>
    /// </summary>
    public static Expression<Func<AgendaItem, bool>> AddressedToViewer(AgendaViewerScope viewer)
    {
        var groupKeys = viewer.ViewerGroupKeys;
        var leadershipKeys = viewer.ViewerLeadershipKeys;
        var memberKey = viewer.ViewerMemberKey;

        return item => item.Assignments.Any(assignment =>
            assignment.TargetType == AgendaTargetType.Kurin ||
            (assignment.TargetType == AgendaTargetType.Group && groupKeys.Contains(assignment.TargetKey)) ||
            (assignment.TargetType == AgendaTargetType.Leadership && leadershipKeys.Contains(assignment.TargetKey)) ||
            (assignment.TargetType == AgendaTargetType.Member && memberKey != null && assignment.TargetKey == memberKey));
    }

    /// <summary>
    /// What the viewer sees: everything addressed to them, plus everything they raised themselves. A
    /// Гуртковий who hands a task to one of his people must still find it on his board, or he has
    /// no way to follow it; the UI marks such items as not his own to do.
    /// </summary>
    public static Expression<Func<AgendaItem, bool>> AssignedToViewer(AgendaViewerScope viewer)
    {
        var addressed = AddressedToViewer(viewer);
        var userKey = viewer.ViewerUserKey;
        if (userKey is null)
        {
            return addressed;
        }

        var parameter = addressed.Parameters[0];
        var raisedByViewer = Expression.Equal(
            Expression.Property(parameter, nameof(AgendaItem.CreatedByUserKey)),
            Expression.Constant(userKey.Value));
        return Expression.Lambda<Func<AgendaItem, bool>>(Expression.OrElse(addressed.Body, raisedByViewer), parameter);
    }

    /// <summary>
    /// What the calendar shows with «Графіки гуртків» on: what the viewer is assigned or raised, plus
    /// every event of a group marked «графік куреня». The extra events are only to look at — RSVP and
    /// the other guards keep using <see cref="IsVisible"/>, which does not include them.
    /// </summary>
    public static Expression<Func<AgendaItem, bool>> AssignedOrOnKurinSchedule(AgendaViewerScope viewer)
    {
        var assigned = AssignedToViewer(viewer);
        var parameter = assigned.Parameters[0];
        Expression<Func<AgendaItem, bool>> onSchedule = item => item.Category != null && item.Category.IsKurinSchedule;
        var scheduleBody = new ParameterSwap(onSchedule.Parameters[0], parameter).Visit(onSchedule.Body);
        return Expression.Lambda<Func<AgendaItem, bool>>(Expression.OrElse(assigned.Body, scheduleBody), parameter);
    }

    private sealed class ParameterSwap(ParameterExpression from, ParameterExpression to) : ExpressionVisitor
    {
        protected override Expression VisitParameter(ParameterExpression node) => node == from ? to : base.VisitParameter(node);
    }

    /// <summary>Whether at least one assignment reaches the viewer; false for an item they only authored.</summary>
    public static bool IsAddressedTo(AgendaItem item, AgendaViewerScope viewer)
        => AddressedToViewer(viewer).Compile()(item);

    /// <summary>
    /// Evaluates the same rule against an item whose <see cref="AgendaItem.Assignments"/> are loaded.
    /// <para>
    /// Compiling the expression costs a fraction of a millisecond and happens once per guarded request,
    /// against a handler that is already waiting on database I/O — cheap next to letting the rule be
    /// written a second time by hand.
    /// </para>
    /// </summary>
    public static bool IsVisible(AgendaItem item, AgendaViewerScope viewer)
        => viewer.CanSeeWholeKurin || AssignedToViewer(viewer).Compile()(item);
}
