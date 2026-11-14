using ProjectK.Common.Dtos.KurinModule;
using ProjectK.Common.Enums.KurinModule;

namespace ProjectK.Common.Dtos.AuthModule;

/// <summary>A kurin this account may stand in.</summary>
/// <param name="Kind">Юнацьке членство чи виховний склад — the same person may be either, in different kurins.</param>
/// <param name="Branches">Every гілка with the kurin's number there, main one first.</param>
public sealed record KurinScopeOptionDto(
    Guid KurinKey,
    int? KurinNumber,
    KurinBranch Branch,
    string? NamedAfter,
    MembershipKind Kind,
    IReadOnlyList<KurinNumbering> Branches);
