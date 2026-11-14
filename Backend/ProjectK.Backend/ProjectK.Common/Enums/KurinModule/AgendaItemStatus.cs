namespace ProjectK.Common.Enums.KurinModule;

/// <summary>
/// Board column of a task. Adding a status is a single enum entry here plus a matching label/colour
/// map on the frontend (agenda-status.config.ts); the value is persisted as int so order is stable.
/// Events keep <see cref="Todo"/> and ignore this on the UI.
/// </summary>
public enum AgendaItemStatus
{
    Todo = 0,
    InProgress = 1,
    Done = 2
}
