using ProjectK.Common.Dtos.KurinModule;
using ProjectK.Common.Enums.KurinModule;

namespace ProjectK.BusinessLogic.Modules.KurinModule.Services;

/// <summary>The mode a requested target is stored with: only a task aimed at more than one person has a choice.</summary>
public static class AgendaTargetModeRules
{
    public static AgendaCompletionMode For(AgendaTargetInput target, AgendaItemKind kind, AgendaCompletionMode? current = null) =>
        kind == AgendaItemKind.Task && target.TargetType != AgendaTargetType.Member
            ? target.CompletionMode ?? current ?? AgendaCompletionMode.Shared
            : AgendaCompletionMode.Shared;
}
