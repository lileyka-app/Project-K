using System.Globalization;
using ProjectK.Common.Entities.KurinModule;
using ProjectK.Common.Models.Audit;
using ProjectK.Common.Models.Enums;

namespace ProjectK.BusinessLogic.Modules.KurinModule.Services;

/// <summary>
/// The fields of a person's card the kurin's journal records. They go by the card's JSON names, so
/// <see cref="ProjectK.Common.Models.Members.MemberDataFields"/> recognises the private ones and the
/// journal records them as changed without their values.
/// </summary>
public static class MemberAuditFields
{
    public const string PlastLevels = "plastLevels";
    public const string ProfileVerification = "profileVerification";

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
        AuditChanges.Added(Of(member).Where(field => field.Field != ProfileVerification).ToArray());

    /// <summary>The fields that differ between two snapshots taken with <see cref="Of"/>.</summary>
    public static IReadOnlyList<AuditChange> Between((string Field, object? Value)[] before, (string Field, object? Value)[] after) =>
        AuditChanges.Of(before.Zip(after, (was, now) => (was.Field, was.Value, now.Value)).ToArray());

    /// <summary>The ступені in the order they were reached, each with its date; null when there are none.</summary>
    public static string? PlastLevelsOf(IEnumerable<PlastLevelHistory> history)
    {
        var levels = history
            .OrderBy(entry => entry.DateAchieved)
            .ThenBy(entry => entry.PlastLevel)
            .Select(entry => $"{PlastLevelNames.Of(entry.PlastLevel)} ({entry.DateAchieved.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)})")
            .ToList();

        return levels.Count == 0 ? null : string.Join("; ", levels);
    }

    /// <summary>The name the journal labels a person's card with.</summary>
    public static string LabelOf(Member member) =>
        string.Join(" ", new[] { member.FirstName, member.LastName }.Where(part => !string.IsNullOrWhiteSpace(part)));
}
