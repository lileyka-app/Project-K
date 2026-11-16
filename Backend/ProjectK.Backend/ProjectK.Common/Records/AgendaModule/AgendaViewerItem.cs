using ProjectK.Common.Enums.AgendaModule;

namespace ProjectK.Common.Records.AgendaModule;

/// <summary>
/// One task or event as a person sees it from one kurin, read for another module: what it is, whether
/// it is theirs, and — for an event — its occurrences in the window that was asked for.
/// </summary>
/// <param name="IsAddressedToViewer">Aimed at the person, their гурток, провід or tag, or at the whole kurin.</param>
/// <param name="IsByViewer">Raised by the person.</param>
/// <param name="GroupTargetKeys">The гуртки it is aimed at, in the order it names them.</param>
/// <param name="ViewerStatus">A task's state for the person; null for an event, or a task the person has no part in.</param>
/// <param name="CanChangeStatus">Whether the person may move the task on the board.</param>
/// <param name="Occurrences">An event's occurrences in the window, in order; empty for a task.</param>
public sealed record AgendaViewerItem(
    Guid AgendaItemKey,
    Guid KurinKey,
    AgendaItemKind Kind,
    string Title,
    string? Description,
    string? Location,
    DateTime? StartUtc,
    DateTime? EndUtc,
    bool IsAllDay,
    bool IsRecurring,
    Guid? AgendaCategoryKey,
    DateTime UpdatedDate,
    bool IsAddressedToViewer,
    bool IsByViewer,
    IReadOnlyList<Guid> GroupTargetKeys,
    AgendaItemStatus? ViewerStatus,
    bool CanChangeStatus,
    IReadOnlyList<AgendaViewerOccurrence> Occurrences);
