using ProjectK.Common.Contracts.Shared;

namespace ProjectK.Common.Events.KurinModule;

/// <summary>An administrator settled a kurin's request to change its гілки.</summary>
/// <param name="Branches">What the kurin has now when approved, what it asked for when not.</param>
public sealed record KurinBranchRequestReviewed(
    Guid KurinBranchRequestKey,
    Guid KurinKey,
    bool Approved,
    string Branches,
    Guid RequesterUserKey,
    Guid? ActorUserKey) : IDomainEvent;
