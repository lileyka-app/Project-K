using ProjectK.Common.Models.Records;
using ProjectK.Common.Models.Enums;

namespace ProjectK.BusinessLogic.Modules.KurinModule.Models;

/// <summary>
/// A kurin as a request names it: enough to say «к. ч. 15 · ім. …», or «5-й курінь УСП» for a senior
/// one, and no more.
/// </summary>
public sealed record MembershipRequestKurin(Guid KurinKey, int? Number, string? NamedAfter, IReadOnlyList<KurinNumbering> Branches)
{
    /// <summary>The kurin as read, or one nobody can find any more: a key and no name.</summary>
    public static MembershipRequestKurin Of(Guid kurinKey, ProjectK.Common.Entities.KurinModule.Kurin? kurin)
        => new(kurinKey, kurin?.Number, kurin?.NamedAfter, kurin is null ? [] : KurinBranchNames.NumberingOf(kurin));
}

/// <summary>
/// One request between kurins as either side reads it. <see cref="Status"/> is <c>Expired</c> for a
/// request still pending past its date. <see cref="OwedToLeave"/> is filled for the answering kurin
/// on a pending «перевести»: while it is above zero the request cannot be approved.
/// </summary>
public sealed record MembershipRequestDto(
    Guid MembershipRequestKey,
    MembershipRequestKind Kind,
    string Status,
    Guid MemberKey,
    string MemberName,
    MembershipRequestKurin FromKurin,
    MembershipRequestKurin ToKurin,
    MembershipKind MembershipKind,
    DateTime CreatedAtUtc,
    DateTime ExpiresAtUtc,
    DateTime? DecidedAtUtc,
    decimal OwedToLeave);

/// <summary>A kurin's requests: those it has to answer and those it sent.</summary>
public sealed record MembershipRequestsResponse(
    IReadOnlyList<MembershipRequestDto> Incoming,
    IReadOnlyList<MembershipRequestDto> Outgoing);

/// <summary>
/// What sending a request did. A person who stands in no kurin has nobody to ask: they are taken in
/// at once and <see cref="Joined"/> is true.
/// </summary>
public sealed record MembershipRequestSent(Guid? MembershipRequestKey, bool Joined);

/// <summary>
/// A former member and the kurins they stand in now, named only by number. Empty when they stand
/// nowhere: then the kurin takes them back without asking anyone.
/// </summary>
public sealed record FormerMemberStanding(FormerMember Former, IReadOnlyList<MembershipRequestKurin> StandsIn);
