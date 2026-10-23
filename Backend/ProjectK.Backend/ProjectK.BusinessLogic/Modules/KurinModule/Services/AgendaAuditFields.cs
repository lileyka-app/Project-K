using ProjectK.Common.Entities.KurinModule.Agenda;
using ProjectK.Common.Models.Audit;
using ProjectK.Common.Models.Enums;

namespace ProjectK.BusinessLogic.Modules.KurinModule.Services;

/// <summary>
/// The fields of a task or event the kurin's journal records, under the names the clients know
/// them by. Its targets are recorded beside them as <c>targets</c>, named by <see cref="AgendaTargetNames"/>.
/// </summary>
public static class AgendaAuditFields
{
    public const string Targets = "targets";

    /// <summary>The item's recorded fields as they stand now; taken before an edit, it is what the edit is measured against.</summary>
    public static (string Field, object? Value)[] Of(AgendaItem item) =>
    [
        ("title", item.Title),
        ("kind", item.Kind),
        ("start", item.StartUtc),
        ("end", item.EndUtc),
        ("location", item.Location),
        ("description", item.Description),
        ("recurrence", item.RecurrenceFrequency == RecurrenceFrequency.None ? null : item.RecurrenceFrequency)
    ];

    /// <summary>The fields that differ between two snapshots taken with <see cref="Of"/>.</summary>
    public static IReadOnlyList<AuditChange> Between((string Field, object? Value)[] before, (string Field, object? Value)[] after) =>
        AuditChanges.Of(before.Zip(after, (was, now) => (was.Field, was.Value, now.Value)).ToArray());

    /// <summary>The item's targets, as <see cref="AgendaTargetNames"/> takes them.</summary>
    public static List<(AgendaTargetType Type, Guid Key)> TargetsOf(AgendaItem item) =>
        item.Assignments.Select(assignment => (assignment.TargetType, assignment.TargetKey)).ToList();
}
