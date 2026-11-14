using ProjectK.Common.Enums.Shared;

namespace ProjectK.BusinessLogic.Modules.RosterModule.Features.Import;

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
