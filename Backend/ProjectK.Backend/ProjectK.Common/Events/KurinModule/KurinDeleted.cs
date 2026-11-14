using ProjectK.Common.Contracts.Shared;

namespace ProjectK.Common.Events.KurinModule;

/// <summary>
/// A kurin is being deleted. Published before the deletion is saved, so whoever keeps rows that name
/// the kurin and that the database will not clear itself removes them on the same unit of work, and
/// they go together with the kurin or not at all.
/// </summary>
public sealed record KurinDeleted(Guid KurinKey) : IDomainEvent;
