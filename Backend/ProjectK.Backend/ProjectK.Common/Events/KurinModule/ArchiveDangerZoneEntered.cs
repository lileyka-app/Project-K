using ProjectK.Common.Contracts.Shared;

namespace ProjectK.Common.Events.KurinModule;

/// <summary>
/// Someone turned on the danger zone of a kurin's archive, where records are deleted for good.
/// The rest of the kurin's leadership hears of it.
/// </summary>
public sealed record ArchiveDangerZoneEntered(Guid KurinKey, string ActorName, Guid ActorUserKey) : IDomainEvent;
