namespace ProjectK.Common.Records.AgendaModule;

/// <summary>An event of a kurin, as another module reads it: what it is called and whom it is aimed at.</summary>
/// <param name="IsAimedAtKurin">Aimed at the whole kurin.</param>
/// <param name="GroupKeys">The гуртки it is aimed at.</param>
/// <param name="MemberKeys">The people it is aimed at by name.</param>
/// <param name="TagKeys">The tags it is aimed at.</param>
public sealed record AgendaEventSummary(
    Guid AgendaItemKey,
    Guid KurinKey,
    string Title,
    Guid? AgendaCategoryKey,
    bool IsAllDay,
    bool IsRecurring,
    bool IsAimedAtKurin,
    IReadOnlyCollection<Guid> GroupKeys,
    IReadOnlyCollection<Guid> MemberKeys,
    IReadOnlyCollection<Guid> TagKeys);
