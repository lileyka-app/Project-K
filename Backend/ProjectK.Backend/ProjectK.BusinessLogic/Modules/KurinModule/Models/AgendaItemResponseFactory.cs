using ProjectK.Common.Models.Authorization;
using ProjectK.BusinessLogic.Modules.KurinModule.Services;
using ProjectK.Common.Entities.KurinModule.Agenda;
using ProjectK.Common.Models.Enums;

namespace ProjectK.BusinessLogic.Modules.KurinModule.Models;

/// <summary>
/// Builds <see cref="AgendaItemResponse"/> from an entity plus name lookups and the viewer context.
/// Kept in one place so the calendar and board return identical shapes and per-viewer flags.
/// </summary>
public static class AgendaItemResponseFactory
{
    public static AgendaItemResponse Create(
        AgendaItem item,
        AgendaViewerContext viewer,
        string kurinLabel,
        IReadOnlyDictionary<Guid, string> groupNames,
        IReadOnlyDictionary<Guid, string> memberNames,
        IReadOnlyDictionary<Guid, string> creatorNames,
        IReadOnlyDictionary<Guid, string> targetLabels,
        IReadOnlyDictionary<Guid, AgendaCategory> categories,
        AgendaRoster roster,
        DateTime? occurrenceStartUtc = null,
        DateTime? occurrenceEndUtc = null)
    {
        AgendaCategory? category = null;
        if (item.AgendaCategoryKey.HasValue)
        {
            categories.TryGetValue(item.AgendaCategoryKey.Value, out category);
        }

        var scope = viewer.ToScope();
        var onSchedule = category?.IsKurinSchedule == true;
        var raisedByViewer = viewer.ViewerUserKey.HasValue && item.CreatedByUserKey == viewer.ViewerUserKey.Value;
        var addressed = AgendaVisibility.IsAddressedTo(item, scope);

        // For a recurring series the calendar shows one row per occurrence: the dates come from the
        // expansion, but the key stays the series key so edit/delete act on the whole series (v1).
        var isInstance = occurrenceStartUtc.HasValue;

        return new AgendaItemResponse
        {
            AgendaItemKey = item.AgendaItemKey,
            KurinKey = item.KurinKey,
            Kind = item.Kind,
            Title = item.Title,
            Description = item.Description,
            Location = item.Location,
            Status = AgendaCompletion.ItemStatus(item, roster),
            ViewerStatus = AgendaCompletion.ViewerStatus(item, viewer, roster),
            // Stamp Kind=Utc so the JSON carries a 'Z'; EF returns these as Unspecified, which would
            // otherwise serialize without an offset and be read as local time by the browser.
            StartUtc = AsUtc(occurrenceStartUtc ?? item.StartUtc),
            EndUtc = AsUtc(isInstance ? occurrenceEndUtc : item.EndUtc),
            IsAllDay = item.IsAllDay,
            CreatedByUserKey = item.CreatedByUserKey,
            CreatedByName = creatorNames.TryGetValue(item.CreatedByUserKey, out var creator) ? creator : null,
            CreatedUtc = DateTime.SpecifyKind(item.CreatedDate, DateTimeKind.Utc),
            UpdatedUtc = DateTime.SpecifyKind(item.UpdatedDate, DateTimeKind.Utc),
            CompletedAtUtc = AsUtc(item.CompletedAtUtc),
            ArchivedAtUtc = AsUtc(item.ArchivedAtUtc),
            ArchivedByName = NameOf(item.ArchivedByUserKey, creatorNames),
            CanEdit = AgendaPermissions.CanManage(item, viewer),
            CanChangeStatus = AgendaPermissions.CanChangeStatus(item, viewer),
            AddressedToViewer = addressed,
            IsKurinSchedule = onSchedule,
            Audience = onSchedule && !addressed && !raisedByViewer ? AgendaAudience.Schedule : AgendaAudience.Assigned,
            CategoryKey = category?.AgendaCategoryKey,
            CategoryName = category?.Name,
            CategoryColorHex = category?.ColorHex,
            CategoryIcon = category?.Icon,
            RecurrenceFrequency = item.RecurrenceFrequency,
            RecurrenceInterval = item.RecurrenceInterval,
            RecurrenceByWeekday = item.RecurrenceByWeekday,
            RecurrenceEndUtc = AsUtc(item.RecurrenceEndUtc),
            RecurrenceCount = item.RecurrenceCount,
            IsRecurrenceInstance = isInstance,
            SeriesStartUtc = AsUtc(item.StartUtc),
            SeriesEndUtc = AsUtc(item.EndUtc),
            Assignments = item.Assignments
                .Select(a => ToDto(item, a, viewer, roster, kurinLabel, groupNames, memberNames, creatorNames, targetLabels))
                .ToList()
        };
    }

    private static AgendaAssignmentDto ToDto(
        AgendaItem item,
        AgendaAssignment assignment,
        AgendaViewerContext viewer,
        AgendaRoster roster,
        string kurinLabel,
        IReadOnlyDictionary<Guid, string> groupNames,
        IReadOnlyDictionary<Guid, string> memberNames,
        IReadOnlyDictionary<Guid, string> userNames,
        IReadOnlyDictionary<Guid, string> targetLabels)
    {
        var mode = AgendaCompletion.ModeOf(assignment);
        var dto = new AgendaAssignmentDto
        {
            AgendaAssignmentKey = assignment.AgendaAssignmentKey,
            TargetType = assignment.TargetType,
            TargetKey = assignment.TargetKey,
            Label = ResolveLabel(assignment, kurinLabel, groupNames, memberNames, targetLabels),
            CompletionMode = mode,
            Status = AgendaCompletion.TargetStatus(assignment, roster),
            StatusChangedByName = NameOf(assignment.StatusChangedByUserKey, userNames),
            StatusChangedAtUtc = AsUtc(assignment.StatusChangedAtUtc),
            CanChangeStatus = AgendaCompletion.CanMoveTarget(item, assignment, viewer)
        };

        if (mode != AgendaCompletionMode.PerMember)
        {
            return dto;
        }

        // Who carries a tag is shown only as far as the viewer may see tags; the target's own state is not.
        var people = assignment.TargetType == AgendaTargetType.Tag
            ? roster.PeopleIn(assignment).Where(person => viewer.SeesTagCarrier(person, roster.GroupOf(person))).ToList()
            : roster.PeopleIn(assignment);
        var parts = AgendaCompletion.PartsByMember(assignment);
        dto.PeopleCount = people.Count;
        dto.DoneCount = people.Count(person => parts.TryGetValue(person, out var part) && part.Status == AgendaItemStatus.Done);

        if (AgendaCompletion.Runs(item, assignment, viewer))
        {
            dto.Parts = people
                .Select(person =>
                {
                    parts.TryGetValue(person, out var part);
                    return new AgendaPartDto
                    {
                        MemberKey = person,
                        Name = memberNames.TryGetValue(person, out var name) ? name : "—",
                        Status = part?.Status ?? AgendaItemStatus.Todo,
                        ChangedByName = part is null ? null : NameOf(part.ChangedByUserKey, userNames),
                        ChangedAtUtc = part is null ? null : AsUtc(part.ChangedAtUtc),
                        CanChangeStatus = AgendaCompletion.CanMovePart(item, assignment, viewer, person)
                    };
                })
                .OrderBy(p => p.Status == AgendaItemStatus.Done)
                .ThenBy(p => p.Name)
                .ToList();
        }

        return dto;
    }

    private static string? NameOf(Guid? userKey, IReadOnlyDictionary<Guid, string> userNames) =>
        userKey is { } key && userNames.TryGetValue(key, out var name) ? name : null;

    /// <summary>Marks a stored-UTC value as <see cref="DateTimeKind.Utc"/> so JSON emits a trailing 'Z'.</summary>
    private static DateTime? AsUtc(DateTime? value) =>
        value.HasValue ? DateTime.SpecifyKind(value.Value, DateTimeKind.Utc) : null;

    private static string ResolveLabel(
        AgendaAssignment assignment,
        string kurinLabel,
        IReadOnlyDictionary<Guid, string> groupNames,
        IReadOnlyDictionary<Guid, string> memberNames,
        IReadOnlyDictionary<Guid, string> targetLabels)
    {
        return assignment.TargetType switch
        {
            AgendaTargetType.Kurin => kurinLabel,
            AgendaTargetType.Group => groupNames.TryGetValue(assignment.TargetKey, out var name) ? name : "—",
            AgendaTargetType.Member => memberNames.TryGetValue(assignment.TargetKey, out var name) ? name : "—",
            AgendaTargetType.Leadership => targetLabels.TryGetValue(assignment.TargetKey, out var name) ? name : "—",
            AgendaTargetType.Tag => targetLabels.TryGetValue(assignment.TargetKey, out var name) ? name : AgendaLookups.TagLabel(null),
            _ => "—"
        };
    }
}
