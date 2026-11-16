using ProjectK.Common.Enums.AgendaModule;

namespace ProjectK.Common.Rules.AgendaModule;

/// <summary>Where a task or an event opens on the client, for notifications and calendar files alike.</summary>
public static class AgendaRouteNames
{
    public static string For(AgendaItemKind kind, Guid kurinKey) =>
        kind == AgendaItemKind.Task ? $"/tasks/{kurinKey}" : $"/calendar/{kurinKey}";
}
