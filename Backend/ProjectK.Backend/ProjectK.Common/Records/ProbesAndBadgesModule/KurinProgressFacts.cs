namespace ProjectK.Common.Records.ProbesAndBadgesModule;

/// <summary>
/// What the probes and вмілості module knows was earned in one kurin — the facts точкування
/// prices. Read whole, kurin by kurin, so the score needs one call and not one per person.
/// </summary>
public sealed record KurinProgressFacts(
    IReadOnlyCollection<BadgeConfirmedRecord> Badges,
    IReadOnlyCollection<ProbePointSignedRecord> ProbePoints,
    IReadOnlyCollection<ProbeClosedRecord> Probes)
{
    public static KurinProgressFacts Empty { get; } = new([], [], []);
}
