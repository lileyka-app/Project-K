namespace ProjectK.Common.Records.KurinModule;

/// <summary>
/// What a провід is shown before taking someone into their kurin: enough to be sure it is the right
/// person, and nothing more. Deliberately no kurins and no contact details — the code is looked up
/// by someone who was given it, and answering "which kurins is this person in" to anyone holding a
/// code would say more about them than they agreed to.
/// </summary>
public sealed record MemberCard(
    Guid MemberKey,
    string PublicId,
    string FirstName,
    string LastName,
    string? ProfilePhotoBlobName,
    int CurrentMembershipCount)
{
    public string FullName => $"{FirstName} {LastName}".Trim();
}
