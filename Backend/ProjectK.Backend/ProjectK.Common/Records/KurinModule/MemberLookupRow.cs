using ProjectK.Common.Records.IdentityModule;

namespace ProjectK.Common.Records.KurinModule;

/// <summary>
/// A person as a picker shows them, with the office that names them in the kurin asked about, or
/// none. The office is read as stored: a виховник closed over a гурток but never seated comes back
/// without one, and counting the assignment as an office is <c>MentorOfficeRules</c>' to do.
/// </summary>
public sealed record MemberLookupRow(
    Guid MemberKey,
    Guid? UserKey,
    string FirstName,
    string? MiddleName,
    string LastName,
    string? ProfilePhotoBlobName,
    MemberOffice? Office);
