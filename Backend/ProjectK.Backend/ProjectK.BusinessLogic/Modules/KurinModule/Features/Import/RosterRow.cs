using ProjectK.BusinessLogic.Modules.KurinModule.Services;
using ProjectK.Common.Dtos.MemberModule;
using ProjectK.Common.Enums.Shared;
using ProjectK.Common.Records.InfrastructureModule;
using ProjectK.Common.Rules.Shared;

namespace ProjectK.BusinessLogic.Modules.KurinModule.Features.Import;

/// <summary>
/// One row of the file after the mapping has been applied to it — still only what the file said,
/// with nothing looked up and nothing decided.
/// </summary>
internal sealed class RosterRow
{
    public int Number { get; init; }

    public string? LastName { get; init; }

    public string? FirstName { get; init; }

    public string? MiddleName { get; init; }

    public DateOnly? DateOfBirth { get; init; }

    public PlastLevel? Level { get; init; }

    public DateOnly? LevelDate { get; init; }

    public string? GroupName { get; init; }

    public int? KurinNumber { get; init; }

    public string? PhoneNumber { get; init; }

    public string? Email { get; init; }

    public string? Address { get; init; }

    public string? School { get; init; }

    /// <summary>Dates of other ступені the file happened to carry.</summary>
    public Dictionary<PlastLevel, DateOnly> LevelDates { get; } = [];

    public string DisplayName => PersonNames.SurnameFirst(LastName, FirstName);

    /// <summary>
    /// Why this row cannot be imported, or null when it can. The required set is IMPORT-01's:
    /// a name, a birthday, and a ступінь **with the day it was reached** — a ступінь without one
    /// is not a fact the system will store. An e-mail address is required too: the import is how
    /// a kurin brings its people in, and a person with no way to sign in is only half brought in.
    /// The kurin is not asked for: the import always lands in the kurin it was started from, and
    /// a column naming another one is only a warning.
    /// </summary>
    public string? Rejection()
    {
        if (string.IsNullOrWhiteSpace(LastName)) return "Немає прізвища.";
        if (string.IsNullOrWhiteSpace(FirstName)) return "Немає імені.";
        if (DateOfBirth is null) return "Немає дати народження або її не вдалося прочитати.";
        if (Level is null) return "Немає пластового ступеня або його не вдалося розпізнати.";
        if (LevelDate is null) return "Ступінь є, а дати його здобуття немає.";
        if (string.IsNullOrWhiteSpace(GroupName)) return "Не вказано гурток.";
        if (string.IsNullOrWhiteSpace(Email)) return "Немає пошти: без неї людині не відкрити акаунт.";
        return null;
    }

    /// <summary>
    /// Every dated ступінь this row knows about. The required pair and a column for the same
    /// ступінь are one entry, not two — they are saying the same thing.
    /// </summary>
    public List<PlastLevelHistoryDto> Levels()
    {
        var byLevel = new Dictionary<PlastLevel, DateOnly>(LevelDates);
        if (Level is { } level && LevelDate is { } reached)
        {
            byLevel[level] = reached;
        }

        return byLevel
            .OrderBy(entry => entry.Value)
            .Select(entry => new PlastLevelHistoryDto
            {
                PlastLevel = entry.Key,
                DateAchieved = entry.Value
            })
            .ToList();
    }

    /// <summary>Reads one row of the file through the mapping the провід confirmed.</summary>
    public static RosterRow From(SheetRow row, IReadOnlyList<ColumnMapping> mapping)
    {
        string? At(int index) => index >= 0 && index < row.Cells.Count
            ? TextRules.TrimOrNull(row.Cells[index])
            : null;

        string? Of(RosterField field) => mapping
            .Where(column => column.Field == field)
            .Select(column => At(column.Index))
            .FirstOrDefault(value => value is not null);

        var parsed = new RosterRow
        {
            Number = row.Number,
            LastName = Of(RosterField.LastName),
            FirstName = Of(RosterField.FirstName),
            MiddleName = Of(RosterField.MiddleName),
            DateOfBirth = RosterValueRules.Date(Of(RosterField.DateOfBirth)),
            Level = RosterValueRules.Level(Of(RosterField.PlastLevel)),
            LevelDate = RosterValueRules.Date(Of(RosterField.PlastLevelDate)),
            GroupName = Of(RosterField.GroupName),
            KurinNumber = RosterValueRules.KurinNumber(Of(RosterField.KurinNumber)),
            // A number that is not one (or not one the file says plainly) is left out, not the row.
            PhoneNumber = PhoneNumberRules.NormalizeLegacy(Of(RosterField.PhoneNumber)) is { Length: > 0 } phone ? phone : null,
            Email = Of(RosterField.Email),
            Address = Of(RosterField.Address),
            School = Of(RosterField.School)
        };

        foreach (var column in mapping.Where(c => c.Field == RosterField.LevelDate && c.Level.HasValue))
        {
            if (RosterValueRules.Date(At(column.Index)) is { } date)
            {
                parsed.LevelDates[column.Level!.Value] = date;
            }
        }

        return parsed;
    }
}
