using ProjectK.Common.Entities.Shared;
using ProjectK.Common.Models.Enums;

namespace ProjectK.Common.Entities.KurinModule.Agenda;

/// <summary>
/// One target an agenda item is assigned to. An item carries many of these so it can be aimed at the
/// kurin, several groups and individual members simultaneously.
/// </summary>
public class AgendaAssignment : Entity
{
    public Guid AgendaAssignmentKey { get; set; } = Guid.NewGuid();
    public Guid AgendaItemKey { get; set; }
    public AgendaTargetType TargetType { get; set; }

    /// <summary>KurinKey, GroupKey or MemberKey, per <see cref="TargetType"/>.</summary>
    public Guid TargetKey { get; set; }

    /// <summary>Whether the target does the task once together or each person their own. Changeable only while nothing has moved.</summary>
    public AgendaCompletionMode CompletionMode { get; set; } = AgendaCompletionMode.Shared;

    /// <summary>
    /// The target's own state. Set directly in the shared modes; in <see cref="AgendaCompletionMode.PerMember"/>
    /// derived from <see cref="Progress"/> over the people in the target whenever a part moves.
    /// </summary>
    public AgendaItemStatus Status { get; set; } = AgendaItemStatus.Todo;

    public Guid? StatusChangedByUserKey { get; set; }

    public DateTime? StatusChangedAtUtc { get; set; }

    public AgendaItem AgendaItem { get; set; } = null!;

    /// <summary>One row per person who has moved their part, in <see cref="AgendaCompletionMode.PerMember"/>; no row is Todo.</summary>
    public ICollection<AgendaAssignmentProgress> Progress { get; set; } = new List<AgendaAssignmentProgress>();
}
