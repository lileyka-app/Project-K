using ProjectK.Common.Contracts.Shared;

namespace ProjectK.Common.Events.KurinModule;

/// <summary>
/// A verified profile was edited, so what was checked no longer matches what is stored. It is news for
/// the провід who verify, not for the person: someone has to look at the data again.
/// </summary>
public sealed record MemberProfileWentStale(
    Guid MemberKey,
    string MemberName,
    Guid? ActorUserKey) : IDomainEvent;
