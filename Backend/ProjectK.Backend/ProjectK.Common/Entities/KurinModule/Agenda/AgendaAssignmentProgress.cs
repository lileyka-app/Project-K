using ProjectK.Common.Entities.Shared;
using ProjectK.Common.Models.Enums;

namespace ProjectK.Common.Entities.KurinModule.Agenda;

/// <summary>
/// One person's part of a task done «кожному окремо». A row appears the first time the part moves; a
/// person with no row has not started. The people counted are those in the target when it is read,
/// so a newcomer starts at Todo and a leaver's row stays as history without counting.
/// </summary>
public class AgendaAssignmentProgress : Entity
{
    public Guid AgendaAssignmentProgressKey { get; set; } = Guid.NewGuid();
    public Guid AgendaAssignmentKey { get; set; }
    public Guid MemberKey { get; set; }
    public AgendaItemStatus Status { get; set; }

    /// <summary>Who moved it — the person, or their провід marking it for them.</summary>
    public Guid ChangedByUserKey { get; set; }

    public DateTime ChangedAtUtc { get; set; }

    public AgendaAssignment Assignment { get; set; } = null!;
}
