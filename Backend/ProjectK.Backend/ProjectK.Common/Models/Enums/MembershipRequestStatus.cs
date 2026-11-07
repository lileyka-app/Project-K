namespace ProjectK.Common.Models.Enums;

/// <summary>
/// Where a request between kurins stands. Stored as a number — the values are frozen. A request
/// still pending after its <c>ExpiresAtUtc</c> has expired; that is read from the date, not stored.
/// </summary>
public enum MembershipRequestStatus
{
    Pending = 0,
    Approved = 1,
    Declined = 2,
    Cancelled = 3,
}
