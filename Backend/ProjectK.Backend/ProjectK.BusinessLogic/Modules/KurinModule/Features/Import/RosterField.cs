using ProjectK.Common.Enums.Shared;

namespace ProjectK.BusinessLogic.Modules.KurinModule.Features.Import;

/// <summary>
/// What a column of someone's roster can mean. The names are the wire contract with the mapping
/// screen; the Ukrainian ones are only labels and headings to guess from.
/// </summary>
public enum RosterField
{
    Ignore = 0,
    LastName,
    FirstName,
    MiddleName,
    DateOfBirth,

    /// <summary>The ступінь as a word — «скоб», «пл. розв.», «учасник».</summary>
    PlastLevel,

    /// <summary>The day that ступінь was reached. Required with it: see IMPORT-01.</summary>
    PlastLevelDate,

    GroupName,
    KurinNumber,
    PhoneNumber,
    Email,
    Address,
    School,

    /// <summary>A column holding the date of one named ступінь. Carries which one.</summary>
    LevelDate
}

/// <summary>
/// One column of the file, and what the провід said it means. <paramref name="Level"/> is set
/// only for <see cref="RosterField.LevelDate"/>.
/// </summary>
public sealed record ColumnMapping(int Index, RosterField Field, PlastLevel? Level = null);

/// <summary>
/// What the mapping screen offers one kurin: the fields a row cannot go without, the ones it may
/// carry, the ступені a date column can be, and whether rows go into гуртки at all. Sent with the
/// preview, so the screen asks exactly what the import will check.
/// </summary>
public sealed record RosterImportShape(
    IReadOnlyList<RosterField> Required,
    IReadOnlyList<RosterField> Optional,
    IReadOnlyList<PlastLevel> LevelDates,
    bool PlacesInGroups);
