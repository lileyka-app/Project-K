using ProjectK.BusinessLogic.Modules.KurinModule.Features.Import;
using ProjectK.Common.Enums.KurinModule;
using ProjectK.Common.Enums.Shared;
using ProjectK.Common.Rules.KurinModule;
using ProjectK.Common.Rules.Shared;

namespace ProjectK.BusinessLogic.Modules.KurinModule.Services;

/// <summary>
/// What the roster import asks of a kurin, by its branch. One place for it, so the check of a row,
/// the fields the mapping screen offers and the template file cannot disagree. A УПЮ kurin places
/// every person in a гурток; a УСП or УПС kurin has none and takes people into the kurin itself —
/// everything else is the same.
/// </summary>
internal static class RosterImportRules
{
    private static readonly RosterField[] YouthRequired =
    [
        RosterField.LastName,
        RosterField.FirstName,
        RosterField.DateOfBirth,
        RosterField.PlastLevel,
        RosterField.PlastLevelDate,
        RosterField.GroupName,
        RosterField.Email
    ];

    private static readonly RosterField[] SeniorRequired =
        [.. YouthRequired.Where(field => field != RosterField.GroupName)];

    /// <summary>
    /// Asked but not required. The kurin column only warns: the import always lands in the kurin
    /// it was started from.
    /// </summary>
    private static readonly RosterField[] Optional =
    [
        RosterField.MiddleName,
        RosterField.KurinNumber,
        RosterField.PhoneNumber
    ];

    /// <summary>The template's headings — each one is what <see cref="RosterHeaderRules"/> reads back as its field.</summary>
    private static readonly IReadOnlyDictionary<RosterField, string> Headings = new Dictionary<RosterField, string>
    {
        [RosterField.LastName] = "Прізвище",
        [RosterField.FirstName] = "Ім'я",
        [RosterField.MiddleName] = "По батькові",
        [RosterField.DateOfBirth] = "Дата народження",
        [RosterField.PlastLevel] = "Пластовий ступінь",
        [RosterField.PlastLevelDate] = "Дата ступеня",
        [RosterField.GroupName] = "Гурток",
        [RosterField.Email] = "Пошта",
        [RosterField.PhoneNumber] = "Телефон"
    };

    /// <summary>Whether each row names a гурток to be placed in.</summary>
    public static bool PlacesInGroups(KurinBranch branch) => !KurinBranchSetRules.IsSenior(branch);

    /// <summary>The fields a row cannot go without, in the order the screen names them.</summary>
    public static IReadOnlyList<RosterField> RequiredFor(KurinBranch branch)
        => PlacesInGroups(branch) ? YouthRequired : SeniorRequired;

    public static IReadOnlyList<RosterField> OptionalFor(KurinBranch branch) => Optional;

    /// <summary>
    /// The ступені a «Дата: …» column can be. Cumulative, as the registry's columns are: a senior kurin
    /// still keeps the day its people were заприсяжені. A senior kurin gets the whole ladder: it is often
    /// УСП and УПС at once, and its main branch alone would leave the сеніорські dates out.
    /// </summary>
    public static IReadOnlyList<PlastLevel> LevelDatesFor(KurinBranch branch)
        => PlacesInGroups(branch) ? PlastLadderRules.DefaultFor(branch) : PlastLadderRules.InOrder;

    /// <summary>What the mapping screen offers this kurin.</summary>
    public static RosterImportShape ShapeFor(KurinBranch branch)
        => new(RequiredFor(branch), OptionalFor(branch), LevelDatesFor(branch), PlacesInGroups(branch));

    /// <summary>
    /// A field the kurin has no use for is not suggested: a «Гурток» column in a senior kurin's
    /// file starts on «Не імпортувати» rather than on a field the screen does not offer.
    /// </summary>
    public static RosterField Offered(RosterField field, KurinBranch branch)
        => field == RosterField.GroupName && !PlacesInGroups(branch) ? RosterField.Ignore : field;

    /// <summary>
    /// The template's header row: the required fields and the phone, in the order a person fills
    /// them in. The kurin column is left out — the file is for this kurin.
    /// </summary>
    public static IReadOnlyList<(RosterField Field, string Heading)> TemplateColumnsFor(KurinBranch branch)
    {
        var required = RequiredFor(branch);
        IEnumerable<RosterField> order =
        [
            RosterField.LastName,
            RosterField.FirstName,
            RosterField.MiddleName,
            RosterField.DateOfBirth,
            RosterField.PlastLevel,
            RosterField.PlastLevelDate,
            RosterField.GroupName,
            RosterField.Email,
            RosterField.PhoneNumber
        ];

        return order
            .Where(field => required.Contains(field) || Optional.Contains(field))
            .Select(field => (field, Headings[field]))
            .ToList();
    }
}
