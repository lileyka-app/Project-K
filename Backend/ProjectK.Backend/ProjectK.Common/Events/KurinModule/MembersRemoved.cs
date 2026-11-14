using ProjectK.Common.Contracts.Shared;

namespace ProjectK.Common.Events.KurinModule;

/// <summary>
/// These people are gone from the system. Whoever keeps anything keyed by a member — progress,
/// history, assignments — clears it in response; the module that removed them does not need to know
/// who that is.
/// </summary>
public sealed record MembersRemoved(IReadOnlyCollection<Guid> MemberKeys) : IDomainEvent;
