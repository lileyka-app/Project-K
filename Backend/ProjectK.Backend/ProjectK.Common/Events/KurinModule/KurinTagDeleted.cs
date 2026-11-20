using ProjectK.Common.Contracts.Shared;

namespace ProjectK.Common.Events.KurinModule;

/// <summary>
/// A kurin's tag is gone, and with it every rate and audience that named it. Published after the
/// deletion is saved: whoever priced or aimed anything by the tag reads the people's tags as they
/// are now.
/// </summary>
public sealed record KurinTagDeleted(Guid KurinKey, Guid KurinTagKey) : IDomainEvent;
