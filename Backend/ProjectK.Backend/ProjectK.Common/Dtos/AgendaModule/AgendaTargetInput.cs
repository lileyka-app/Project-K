using ProjectK.Common.Enums.AgendaModule;

namespace ProjectK.Common.Dtos.AgendaModule;

/// <summary>
/// One selected "Assign for" target. TargetKey is a KurinKey, GroupKey or MemberKey per TargetType.
/// </summary>
public sealed class AgendaTargetInput
{
    public AgendaTargetType TargetType { get; set; }
    public Guid TargetKey { get; set; }

    /// <summary>
    /// Ignored for a member target, which is always one person's. Left out, a new target is
    /// <see cref="AgendaCompletionMode.Shared"/> and a kept one keeps what it has — so a client that
    /// re-states targets without modes (a drag on the calendar) never reshapes them.
    /// </summary>
    public AgendaCompletionMode? CompletionMode { get; set; }
}
