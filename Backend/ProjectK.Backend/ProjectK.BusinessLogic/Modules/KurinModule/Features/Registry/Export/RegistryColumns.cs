using ProjectK.BusinessLogic.Modules.KurinModule.Models;
using ProjectK.Common.Interfaces.Modules.InfrastructureModule;
using ProjectK.Common.Models.Enums;

namespace ProjectK.BusinessLogic.Modules.KurinModule.Features.Registry.Export;

/// <summary>
/// What each registry column id means. The ids are the frontend's — the screen owns which columns
/// exist and what they are called, and this is the same list on the writing side.
/// <para>
/// Кожен ступінь — колонка <c>level:&lt;PlastLevel&gt;</c>, тож новий ступінь з'являється тут сам,
/// щойно потрапляє в enum. Іменовані колонки доводиться додавати руками; колонка, якої тут немає,
/// просто не вивантажується.
/// </para>
/// <para>
/// Підписи ступенів беруться з <see cref="PlastLevelNames"/> — того самого місця, звідки їх бере
/// звіт куреня. Свою таблицю тут тримати не можна: два підписи одного ступеня в одному релізі
/// вже траплялися.
/// </para>
/// </summary>
internal static class RegistryColumns
{
    private sealed record Column(string Header, Func<MemberResponse, SheetCell> ReadCell);

    private static readonly IReadOnlyDictionary<string, Column> Named =
        new Dictionary<string, Column>(StringComparer.Ordinal)
        {
            ["dateOfBirth"] = new("Дата народження", m => SheetCell.Of(m.DateOfBirth)),
            ["plastLevel"] = new("Пластовий ступінь", m => SheetCell.Of(LevelName(m.LatestPlastLevel))),
            ["groupName"] = new("Гурток", m => SheetCell.Of(m.GroupName)),
            ["mentoredGroups"] = new("Гурток (закріплення)", m => SheetCell.Of(
                m.MentoredGroupNames.Count == 0 ? null : string.Join(", ", m.MentoredGroupNames))),
            ["phoneNumber"] = new("Телефон", m => SheetCell.Of(m.PhoneNumber)),
            ["email"] = new("Пошта", m => SheetCell.Of(m.Email)),
            ["address"] = new("Адреса", m => SheetCell.Of(m.Address)),
            ["school"] = new("Школа", m => SheetCell.Of(m.School))
        };

    /// <summary>The column's heading, or null when nothing here answers to that id.</summary>
    public static string? HeaderFor(string columnId)
    {
        if (Named.TryGetValue(columnId, out var named))
        {
            return named.Header;
        }

        return TryLevel(columnId, out var level) ? PlastLevelNames.Of(level) : null;
    }

    /// <summary>This person's value in that column.</summary>
    public static SheetCell Read(string columnId, MemberResponse member)
    {
        if (Named.TryGetValue(columnId, out var named))
        {
            return named.ReadCell(member);
        }

        if (!TryLevel(columnId, out var level))
        {
            return SheetCell.Of((string?)null);
        }

        var reached = member.PlastLevelHistories.FirstOrDefault(history => history.PlastLevel == level);
        return SheetCell.Of(reached?.DateAchieved);
    }

    private static bool TryLevel(string columnId, out PlastLevel level)
    {
        level = default;
        const string prefix = "level:";
        return columnId.StartsWith(prefix, StringComparison.Ordinal)
               && Enum.TryParse(columnId[prefix.Length..], out level)
               && Enum.IsDefined(level);
    }

    private static string? LevelName(PlastLevel? level)
        => level.HasValue ? PlastLevelNames.Of(level.Value) : null;
}
