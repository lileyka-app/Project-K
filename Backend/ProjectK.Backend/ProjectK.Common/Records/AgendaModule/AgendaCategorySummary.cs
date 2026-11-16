namespace ProjectK.Common.Records.AgendaModule;

/// <summary>A group of events of the kurin, as other modules name and colour it.</summary>
public sealed record AgendaCategorySummary(
    Guid AgendaCategoryKey,
    Guid KurinKey,
    string Name,
    string ColorHex,
    string? Icon,
    bool RsvpRequired,
    bool IsKurinSchedule,
    int? DefaultDurationMinutes);
