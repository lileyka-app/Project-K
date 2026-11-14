using ProjectK.Common.Contracts.Shared;

namespace ProjectK.Common.Events.MemberModule;

/// <summary>
/// A person's record was tied to the account they sign in with. Anything that keeps a copy of that
/// link — a membership does, so authorization never has to read the person — updates itself here.
/// </summary>
public sealed record MemberAccountLinked(Guid MemberKey, Guid UserKey) : IDomainEvent;
