using System.Globalization;
using ProjectK.Common.Dtos.AuditModule;
using ProjectK.Common.Entities.MemberModule;
using ProjectK.Common.Enums.AuditModule;
using ProjectK.Common.Records.AuditModule;
using ProjectK.Common.Rules.AuditModule;
using ProjectK.Common.Rules.Shared;

namespace ProjectK.BusinessLogic.Modules.MemberModule.Services;

/// <summary>
/// The fields of a person's card the kurin's journal records. They go by the card's JSON names, so
/// <see cref="ProjectK.Common.Rules.MemberModule.MemberDataFieldRules"/> recognises the private ones and the
/// journal records them as changed without their values.
/// </summary>
public static class MemberTrail
{
    public const string PlastLevels = "plastLevels";
    public const string ProfileVerification = "profileVerification";

    private const string PendingMark = " — чекає підтвердження";

    /// <summary>
    /// The card as it stands now. Taken before an edit it is what the edit is measured against, so
    /// it copies everything out — the ступені included, which an edit changes in place.
    /// </summary>
    public static (string Field, object? Value)[] Of(Member member) =>
    [
        ("firstName", member.FirstName),
        ("middleName", member.MiddleName),
        ("lastName", member.LastName),
        ("email", member.Email),
        ("phoneNumber", member.PhoneNumber),
        ("dateOfBirth", member.DateOfBirth),
        ("address", member.Address),
        ("school", member.School),
        ("motherFirstName", member.MotherFirstName),
        ("motherLastName", member.MotherLastName),
        ("motherPhone", member.MotherPhone),
        ("fatherFirstName", member.FatherFirstName),
        ("fatherLastName", member.FatherLastName),
        ("fatherPhone", member.FatherPhone),
        ("cardExtras", member.CardExtras),
        (PlastLevels, PlastLevelsOf(member.PlastLevelHistory)),
        (ProfileVerification, member.ProfileVerificationStatus)
    ];

    /// <summary>A new card: every field it was made with, the verification it has not had yet left out.</summary>
    public static IReadOnlyList<AuditChange> Added(Member member) =>
        AuditChangeRules.Added(Of(member).Where(field => field.Field != ProfileVerification).ToArray());

    /// <summary>The fields that differ between two snapshots taken with <see cref="Of"/>.</summary>
    public static IReadOnlyList<AuditChange> Between((string Field, object? Value)[] before, (string Field, object? Value)[] after) =>
        AuditChangeRules.Of(before.Zip(after, (was, now) => (was.Field, was.Value, now.Value)).ToArray());

    /// <summary>
    /// The journal entry for a profile write: a new card with what it was made with, an edit with what
    /// it changed, and null for an edit that changed nothing. A new person is not in any kurin yet, so
    /// the entry is filed under the one they are being placed in; an edit goes to the kurin the request
    /// acts in, or to where the person stands when it acts in none. <c>before</c> is the card taken
    /// with <see cref="Of"/> before the edit, null for a new card.
    /// </summary>
    public static AuditRecord? RecordOf(
        Member member,
        (string Field, object? Value)[]? before,
        Guid kurinKey,
        Guid? actingKurinKey)
    {
        var changes = before is null
            ? Added(member)
            : Between(before, Of(member));
        if (changes.Count == 0)
        {
            return null;
        }

        return new AuditRecord(
            AuditArea.Member,
            before is null ? AuditActionNames.Member.Added : AuditActionNames.Member.Changed,
            member.MemberKey,
            LabelOf(member),
            member.MemberKey,
            changes,
            KurinKey: before is null ? kurinKey : actingKurinKey ?? kurinKey);
    }

    /// <summary>
    /// The ступені in the order they were reached, each with its date and one that waits marked so;
    /// null when there are none. The mark is what makes a confirmation a change the journal sees.
    /// </summary>
    public static string? PlastLevelsOf(IEnumerable<PlastLevelHistory> history)
    {
        var levels = history
            .OrderBy(entry => entry.DateAchieved)
            .ThenBy(entry => entry.PlastLevel)
            .Select(entry => $"{PlastLevelNames.Of(entry.PlastLevel)} ({entry.DateAchieved.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)})"
                + (entry.AwaitsConfirmation ? PendingMark : string.Empty))
            .ToList();

        return levels.Count == 0 ? null : string.Join("; ", levels);
    }

    /// <summary>The name the journal labels a person's card with.</summary>
    public static string LabelOf(Member member) =>
        string.Join(" ", new[] { member.FirstName, member.LastName }.Where(part => !string.IsNullOrWhiteSpace(part)));
}
