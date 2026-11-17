namespace ProjectK.Common.Records.MemberModule;

/// <summary>
/// Just enough of a person to tell whether a row of someone else's roster is already them. Not a
/// summary and not a card: this exists for matching, and every field on it is one people are matched
/// by.
/// </summary>
public sealed record MemberIdentity(
    Guid MemberKey,
    string FirstName,
    string LastName,
    string Email,
    string PhoneNumber,
    DateOnly DateOfBirth);
