using ProjectK.Common.Enums.MemberModule;


namespace ProjectK.Common.Dtos.MemberModule;

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
    MembershipRequestKurinDto? FromKurin,
    MembershipRequestKurinDto ToKurin,
    MembershipKind MembershipKind,
    DateTime CreatedAtUtc,
    DateTime ExpiresAtUtc,
    DateTime? DecidedAtUtc,
    decimal OwedToLeave);
