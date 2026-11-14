namespace ProjectK.Common.Enums.AgendaModule;

/// <summary>
/// How a task aimed at a group of people — a гурток, the kurin, a провід — is done. Chosen per target
/// by the author; a <see cref="AgendaTargetType.Member"/> target is always <see cref="Shared"/>.
/// </summary>
public enum AgendaCompletionMode
{
    /// <summary>One result for the whole target, moved by those who run it (its провід, the author, the whole kurin).</summary>
    Shared = 0,

    /// <summary>One result for the whole target, which anyone in it may move.</summary>
    SharedByAnyone = 1,

    /// <summary>Everyone in the target does their own; the target is done once all of them are.</summary>
    PerMember = 2
}
