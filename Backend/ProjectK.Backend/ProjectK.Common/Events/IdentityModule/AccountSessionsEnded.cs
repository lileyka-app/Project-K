using ProjectK.Common.Contracts.Shared;
using ProjectK.Common.Enums.IdentityModule;

namespace ProjectK.Common.Events.IdentityModule;

/// <summary>
/// Every session of an account was ended after a security change or a suspension. Anything else that
/// grants access without a session (the calendar link) ends with them. Published once the sessions
/// are revoked and stored.
/// </summary>
public sealed record AccountSessionsEnded(Guid UserKey, SessionEndReason Reason) : IDomainEvent;
