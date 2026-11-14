using System.Globalization;
using ProjectK.Common.Contracts.KurinModule;
using ProjectK.Common.Enums.AuthModule;
using ProjectK.Common.Enums.KurinModule;
using ProjectK.Common.Records.KurinModule;

namespace ProjectK.Common.Rules.KurinModule;

/// <summary>
/// The one table of which field of a person belongs to which group of their data. A new private
/// field is added here; the mask, write protection and the journal's redaction all read it.
/// </summary>
public static class MemberDataFieldRules
{
    /// <summary>Every group, in the order the card shows them.</summary>
    public static readonly IReadOnlyList<MemberDataGroupInfo> All =
    [
        new(MemberDataGroup.Contacts, "contacts", KurinCapability.SeeContacts, ["email", "phoneNumber"]),
        new(MemberDataGroup.BirthDate, "birthDate", KurinCapability.SeeBirthDate, ["dateOfBirth"]),
        new(MemberDataGroup.Birthday, "birthday", KurinCapability.SeeBirthday, ["birthday"]),
        new(MemberDataGroup.Address, "address", KurinCapability.SeeAddress, ["address", "school"]),
        new(MemberDataGroup.Parents, "parents", KurinCapability.SeeParents,
            ["motherFirstName", "motherLastName", "motherPhone", "fatherFirstName", "fatherLastName", "fatherPhone"]),
        new(MemberDataGroup.Extras, "extras", KurinCapability.SeeCardExtras, ["cardExtras"])
    ];

    private static readonly Dictionary<MemberDataGroup, MemberDataGroupInfo> ByGroup =
        All.ToDictionary(info => info.Group);

    private static readonly Dictionary<string, MemberDataGroup> ByField =
        All.SelectMany(info => info.Fields.Select(field => (field, info.Group)))
            .ToDictionary(pair => pair.field, pair => pair.Group, StringComparer.OrdinalIgnoreCase);

    /// <summary>The row of a group.</summary>
    public static MemberDataGroupInfo Of(MemberDataGroup group) => ByGroup[group];

    /// <summary>The group a response field belongs to, or null for a field nobody hides.</summary>
    public static MemberDataGroup? GroupOf(string field) =>
        ByField.TryGetValue(field, out var group) ? group : null;

    /// <summary>The day and month of a date of birth as <c>MM-dd</c>.</summary>
    public static string? BirthdayOf(DateOnly? dateOfBirth) =>
        dateOfBirth?.ToString("MM-dd", CultureInfo.InvariantCulture);

    /// <summary>Blanks the fields of <paramref name="group"/> that <paramref name="target"/> carries.</summary>
    public static void Hide(IMemberPersonalData target, MemberDataGroup group)
    {
        switch (group)
        {
            case MemberDataGroup.Contacts:
                target.Email = string.Empty;
                target.PhoneNumber = string.Empty;
                break;
            case MemberDataGroup.BirthDate:
                target.DateOfBirth = null;
                break;
            case MemberDataGroup.Birthday:
                target.Birthday = null;
                break;
            case MemberDataGroup.Address:
                target.Address = null;
                target.School = null;
                break;
            case MemberDataGroup.Parents when target is IHasParentContacts parents:
                parents.SetAllParents(null);
                break;
            case MemberDataGroup.Extras when target is IHasCardExtras extras:
                extras.CardExtras = new Dictionary<string, string>();
                break;
        }
    }
}
