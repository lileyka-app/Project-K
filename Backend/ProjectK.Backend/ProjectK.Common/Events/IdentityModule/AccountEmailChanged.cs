using ProjectK.Common.Contracts.Shared;

namespace ProjectK.Common.Events.IdentityModule;

/// <summary>
/// An account confirmed a new address. The account is the source of truth for contact details; the
/// person linked to it, if any, takes the address over.
/// </summary>
public sealed record AccountEmailChanged(Guid UserKey, string Email) : IDomainEvent;
