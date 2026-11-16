using ProjectK.Common.Dtos.AgendaModule;
using ProjectK.Common.Dtos.AgendaModule.Requests;
using ProjectK.Common.Enums.AgendaModule;

namespace ProjectK.BusinessLogic.Modules.AgendaModule.Services;

/// <summary>The mode a requested target is stored with: only a task aimed at more than one person has a choice.</summary>
public static class AgendaTargetModeRules
{
    public static AgendaCompletionMode For(AgendaTargetInput target, AgendaItemKind kind, AgendaCompletionMode? current = null) =>
        kind == AgendaItemKind.Task && target.TargetType != AgendaTargetType.Member
            ? target.CompletionMode ?? current ?? AgendaCompletionMode.Shared
            : AgendaCompletionMode.Shared;
}
