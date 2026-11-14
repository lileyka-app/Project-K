using ProjectK.Common.Enums.AgendaModule;

namespace ProjectK.BusinessLogic.Modules.InfrastructureModule.Services;

/// <summary>
/// Where an agenda change turns into an inbox entry. The route and the wording are worked out here
/// from what the event says — a task belongs to the board, an event to the calendar — so the agenda
/// itself no longer holds a screen path.
/// </summary>
internal static class AgendaNotificationNames
{
    public static string RouteFor(AgendaItemKind kind, Guid kurinKey) =>
        kind == AgendaItemKind.Task ? $"/tasks/{kurinKey}" : $"/calendar/{kurinKey}";

    public static string KindWord(AgendaItemKind kind) =>
        kind == AgendaItemKind.Task ? "Задачу" : "Подію";
}
