namespace ProjectK.Common.Enums.AgendaModule;

/// <summary>
/// Distinguishes a plain calendar event from a trackable board task. Both kinds live in one
/// <see cref="ProjectK.Common.Entities.AgendaModule.AgendaItem"/> so the calendar can render
/// either, while only tasks flow through the Todo/InProgress/Done board.
/// </summary>
public enum AgendaItemKind
{
    Event = 0,
    Task = 1
}
