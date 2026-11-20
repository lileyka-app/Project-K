using ProjectK.Common.Rules.Shared;

namespace ProjectK.Common.Records.MemberModule;

/// <summary>
/// What another module is allowed to know about a person: enough to name them and reach their
/// account. Everything else — the profile, the levels, the verification — stays inside the member's
/// own module and is read through its own use cases. Where the person stands is the kurin's to say
/// (<c>IMembershipDirectory</c>), not the person's.
/// </summary>
public sealed record MemberSummary(
    Guid MemberKey,
    Guid? UserKey,
    string FirstName,
    string LastName,
    string Email,
    string? ProfilePhotoBlobName)
{
    /// <summary>The person's name as it is shown, empty when neither part is set.</summary>
    public string FullName => PersonNames.FirstLast(FirstName, LastName);
}
