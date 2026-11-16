using ProjectK.Common.Enums.AgendaModule;

namespace ProjectK.Common.Records.AgendaModule;

/// <summary>What a read of a person's dated agenda covers.</summary>
/// <param name="Zone">The kurin's zone, which a series' days are counted in.</param>
/// <param name="Kind">Only tasks or only events; null for both.</param>
/// <param name="IncludeKurinSchedules">Also the kurin's schedule events the person may see but is not addressed by.</param>
/// <param name="WithAnswers">Also the person's answer to every event occurrence.</param>
public sealed record AgendaWindow(
    DateTime FromUtc,
    DateTime ToUtc,
    TimeZoneInfo Zone,
    AgendaItemKind? Kind,
    bool IncludeKurinSchedules = false,
    bool WithAnswers = false);
