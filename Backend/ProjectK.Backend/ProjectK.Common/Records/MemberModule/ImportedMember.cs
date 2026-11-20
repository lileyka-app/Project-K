using ProjectK.Common.Dtos.MemberModule;

namespace ProjectK.Common.Records.MemberModule;

/// <summary>
/// A person a roster brings in whom the system has never seen: what the file said about them, and
/// the kurin and гурток they are placed in. <paramref name="GroupKey"/> is null only when the
/// гурток does not exist yet.
/// </summary>
public sealed record ImportedMember(
    Guid KurinKey,
    Guid? GroupKey,
    string FirstName,
    string MiddleName,
    string LastName,
    string Email,
    string PhoneNumber,
    DateOnly DateOfBirth,
    string? Address,
    string? School,
    IReadOnlyList<PlastLevelHistoryDto> PlastLevelHistories);
