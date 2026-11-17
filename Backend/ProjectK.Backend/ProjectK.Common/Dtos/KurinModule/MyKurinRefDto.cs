using ProjectK.Common.Dtos.KurinModule;
using ProjectK.Common.Rules.KurinModule;

namespace ProjectK.Common.Dtos.KurinModule;

/// <summary>The kurin a row belongs to — shown only when the person stands in more than one.</summary>
public sealed record MyKurinRefDto
{
    public required Guid KurinKey { get; init; }
    public required int? KurinNumber { get; init; }
    /// <summary>Every гілка with the kurin's number there, main one first.</summary>
    public required IReadOnlyList<KurinNumbering> Branches { get; init; }
    public required string? NamedAfter { get; init; }
    /// <summary>Whether this is the kurin the token acts in; what can be done from the dashboard depends on it.</summary>
    public required bool IsCurrent { get; init; }

    /// <summary>The kurin of <paramref name="membership"/>, marked current when the token acts in it.</summary>
    public static MyKurinRefDto Of(MembershipRecord membership, Guid? currentKurinKey) => new()
    {
        KurinKey = membership.KurinKey,
        KurinNumber = membership.KurinNumber,
        Branches = [.. KurinBranchSetRules.Ordered(membership.Numbering, n => n.Branch)],
        NamedAfter = membership.KurinNamedAfter,
        IsCurrent = membership.KurinKey == currentKurinKey
    };
}
