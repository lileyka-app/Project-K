using ProjectK.Common.Entities.AgendaModule;
using ProjectK.Common.Enums.AgendaModule;

namespace ProjectK.BusinessLogic.Modules.AgendaModule.Services;

/// <summary>Navigation targets for agenda notifications, matched to the frontend routes.</summary>
public static class AgendaRouteRules
{
    public static string For(AgendaItem item) =>
        item.Kind == AgendaItemKind.Task ? $"/tasks/{item.KurinKey}" : $"/calendar/{item.KurinKey}";
}
