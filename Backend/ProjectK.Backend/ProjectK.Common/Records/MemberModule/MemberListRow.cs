using ProjectK.Common.Entities.MemberModule;
using ProjectK.Common.Enums.MemberModule;
using ProjectK.Common.Enums.Shared;

namespace ProjectK.Common.Records.MemberModule;

/// <summary>
/// One person on a roster as the list read finds them, placed by one membership: who they are, the
/// ступені they reached, their open перестороги, and what they hold in that membership's kurin.
/// Projected in one query for a whole kurin, without the card's include graph.
/// <para>
/// Facts only, read as stored. Who counts as кадра, which office names the person, and which
/// ступінь is their latest are decided by the member module from these, so the roster and the
/// звіт куреня answer by the same rules (<c>KurinRosterRules</c>). What the viewer may see is
/// masked afterwards.
/// </para>
/// </summary>
public sealed record MemberListRow
{
    public required Guid MemberKey { get; init; }
    public required Guid? GroupKey { get; init; }

    /// <summary>The гурток's name; the registry prints it for a whole kurin at once.</summary>
    public required string? GroupName { get; init; }
    public required Guid KurinKey { get; init; }
    public required Guid? UserKey { get; init; }
    public required string FirstName { get; init; }
    public required string? MiddleName { get; init; }
    public required string LastName { get; init; }
    public required string Email { get; init; }
    public required string PhoneNumber { get; init; }
    public required DateOnly? DateOfBirth { get; init; }
    public required string? Address { get; init; }
    public required string? School { get; init; }

    /// <summary>The level stored on the record itself, for people whose history is older than it.</summary>
    public required PlastLevel? StoredPlastLevel { get; init; }
    public required string? ProfilePhotoBlobName { get; init; }
    public required MemberProfileVerificationStatus ProfileVerificationStatus { get; init; }
    public required DateTime? ProfileVerifiedAtUtc { get; init; }
    public required Guid? ProfileVerifiedByUserKey { get; init; }
    public required string? ProfileVerificationNote { get; init; }

    /// <summary>
    /// Every ступінь this person has reached, with the date. Read here and not only on the card
    /// because the registry is a column per ступінь across the whole kurin.
    /// </summary>
    public required List<PlastLevelHistory> PlastLevels { get; init; }

    /// <summary>The offices held now in this kurin — its own and its гуртки's. One held elsewhere is not this kurin's to show.</summary>
    public required List<MemberListOffice> Offices { get; init; }

    /// <summary>
    /// The person's закріплення за гуртками of this kurin, by гурток name. Kept on the account, so a
    /// person without one has none.
    /// </summary>
    public required List<MemberListAssignment> Assignments { get; init; }

    /// <summary>The перестороги not revoked.</summary>
    public required List<MemberWarning> Warnings { get; init; }
}
