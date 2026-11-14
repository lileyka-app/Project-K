using ProjectK.Common.Contracts.Shared;

namespace ProjectK.Common.Events.UsersModule;

/// <summary>An account changed its phone number; the person linked to it, if any, takes it over.</summary>
public sealed record AccountPhoneChanged(Guid UserKey, string PhoneNumber) : IDomainEvent;
