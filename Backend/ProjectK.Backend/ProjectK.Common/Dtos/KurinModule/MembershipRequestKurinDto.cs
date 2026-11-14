using ProjectK.Common.Models.Records;

namespace ProjectK.Common.Dtos.KurinModule;

/// <summary>
/// A kurin as a request names it: enough to say «к. ч. 15 · ім. …», or «5-й курінь УСП» for a senior
/// one, and no more. A kurin nobody can find any more is a key and no name.
/// </summary>
public sealed record MembershipRequestKurinDto(Guid KurinKey, int? Number, string? NamedAfter, IReadOnlyList<KurinNumbering> Branches);
