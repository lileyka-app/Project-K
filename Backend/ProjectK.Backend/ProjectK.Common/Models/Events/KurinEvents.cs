using ProjectK.Common.Interfaces;

namespace ProjectK.Common.Models.Events;

/// <summary>
/// A senior kurin asked the administrators to change its гілки. Carries the wording of both sides,
/// «Орден Хрестоносців» · 5-й курінь УСП and «5-й курінь УСП · 20-й курінь УПС», so whoever tells
/// the administrators does not have to read the kurin back.
/// </summary>
public sealed record KurinBranchRequestSubmitted(
    Guid KurinBranchRequestKey,
    Guid KurinKey,
    string Kurin,
    string Wanted,
    Guid ActorUserKey) : IDomainEvent;

/// <summary>An administrator settled a kurin's request to change its гілки.</summary>
/// <param name="Branches">What the kurin has now when approved, what it asked for when not.</param>
public sealed record KurinBranchRequestReviewed(
    Guid KurinBranchRequestKey,
    Guid KurinKey,
    bool Approved,
    string Branches,
    Guid RequesterUserKey,
    Guid? ActorUserKey) : IDomainEvent;

/// <summary>
/// Someone turned on the danger zone of a kurin's archive, where records are deleted for good.
/// The rest of the kurin's leadership hears of it.
/// </summary>
public sealed record ArchiveDangerZoneEntered(Guid KurinKey, string ActorName, Guid ActorUserKey) : IDomainEvent;
