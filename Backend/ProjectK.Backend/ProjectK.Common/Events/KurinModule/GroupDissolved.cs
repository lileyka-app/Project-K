using ProjectK.Common.Contracts.Shared;

namespace ProjectK.Common.Events.KurinModule;

/// <summary>
/// A гурток is dissolved at <paramref name="DissolvedAtUtc"/>. Published before the dissolution is
/// saved, so whoever keeps anything tied to the гурток closes it on the same unit of work and it commits
/// with the dissolution.
/// </summary>
public sealed record GroupDissolved(Guid KurinKey, Guid GroupKey, DateTime DissolvedAtUtc) : IDomainEvent;
