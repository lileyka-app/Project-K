using ProjectK.Common.Contracts.Shared;

namespace ProjectK.Common.Events.KurinModule;

/// <summary>
/// A kurin's tag is being deleted. Published before the deletion is saved, so whoever names the tag by
/// key drops it on the same unit of work and it commits with the deletion. <see cref="KurinTagDeleted"/>
/// follows once it is saved, for whoever has to read the people's tags as they are after it.
/// </summary>
public sealed record KurinTagRemoved(Guid KurinKey, Guid KurinTagKey) : IDomainEvent;
