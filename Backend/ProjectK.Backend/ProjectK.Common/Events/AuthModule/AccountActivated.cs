using ProjectK.Common.Contracts.Shared;
using ProjectK.Common.Records.MemberModule;

namespace ProjectK.Common.Events.AuthModule;

/// <summary>
/// An invited account set its password and is in use. The member side makes sure a person stands
/// behind it: links the record that already carries the address, or opens one from what the account
/// knows. Published before the activation is saved, so the person commits with it rather than half
/// of it.
/// </summary>
public sealed record AccountActivated(MemberForAccount Account) : IDomainEvent;
