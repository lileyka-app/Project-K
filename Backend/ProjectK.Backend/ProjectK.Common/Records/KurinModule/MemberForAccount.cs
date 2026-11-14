namespace ProjectK.Common.Records.KurinModule;

/// <summary>
/// The person behind a freshly activated account, as the account knows them. Used only when an
/// account arrives without a member record and one has to be opened for it.
/// </summary>
public sealed record MemberForAccount(
    Guid UserKey,
    string Email,
    string FirstName,
    string LastName,
    string PhoneNumber,
    DateOnly DateOfBirth,
    Guid KurinKey);
