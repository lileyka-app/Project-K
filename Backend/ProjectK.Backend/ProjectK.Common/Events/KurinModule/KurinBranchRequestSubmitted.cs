using ProjectK.Common.Contracts.Shared;

namespace ProjectK.Common.Events.KurinModule;

/// <summary>
/// A senior kurin asked the administrators to change its гілки. Carries the wording of both sides,
/// «Сторожі Горган» · 5-й курінь УСП and «5-й курінь УСП · 20-й курінь УПС», so whoever tells
/// the administrators does not have to read the kurin back.
/// </summary>
public sealed record KurinBranchRequestSubmitted(
    Guid KurinBranchRequestKey,
    Guid KurinKey,
    string Kurin,
    string Wanted,
    Guid ActorUserKey) : IDomainEvent;
