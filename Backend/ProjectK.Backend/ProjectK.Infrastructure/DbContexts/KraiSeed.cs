using ProjectK.Common.Entities.KraiModule;

namespace ProjectK.Infrastructure.DbContexts;

/// <summary>
/// The краї every instance starts with: the ones the global Plast site lists. A край registered
/// later is added by an admin (<c>POST api/krai</c>), not here: a code added here after an admin
/// created it would make the migration fail on a duplicate key.
/// </summary>
internal static class KraiSeed
{
    /// <summary>
    /// One край as seeded. <paramref name="Profile"/> is the JSON of its first revision; Україна
    /// and Чехія were seeded with an empty one, and a seeded revision is never rewritten, because
    /// an admin may have saved newer ones over it.
    /// </summary>
    public sealed record Entry(string Code, string Name, DateTime SeededAtUtc, Guid RevisionKey, string Profile);

    private static readonly DateTime FirstSeed = new(2026, 10, 14, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime PlastSiteSeed = new(2026, 10, 18, 0, 0, 0, DateTimeKind.Utc);

    public static readonly IReadOnlyList<Entry> All =
    [
        new(Krai.Ukraine, "Україна", FirstSeed, new Guid("6f0c1a52-3d4e-4b8a-9a71-2f3c4d5e6a01"), "{}"),
        new(Krai.Czechia, "Чехія", FirstSeed, new Guid("6f0c1a52-3d4e-4b8a-9a71-2f3c4d5e6a02"), "{}"),
        Later(0x03, Krai.Poland, "Польща", "PLN", "Europe/Warsaw", "pl"),
        Later(0x04, "au", "Австралія", "AUD", "Australia/Sydney"),
        Later(0x05, "at", "Австрія", "EUR", "Europe/Vienna"),
        Later(0x06, "ar", "Аргентина", "ARS", "America/Argentina/Buenos_Aires"),
        Later(0x07, "gb", "Велика Британія", "GBP", "Europe/London"),
        Later(0x08, "ca", "Канада", "CAD", "America/Toronto"),
        Later(0x09, "de", "Німеччина", "EUR", "Europe/Berlin"),
        Later(0x0a, "us", "Сполучені Штати Америки", "USD", "America/New_York"),
        Later(0x0b, "be", "Бельгія", "EUR", "Europe/Brussels"),
        Later(0x0c, "ge", "Грузія", "GEL", "Asia/Tbilisi"),
        Later(0x0d, "dk", "Данія", "DKK", "Europe/Copenhagen"),
        Later(0x0e, "is", "Ісландія", "ISK", "Atlantic/Reykjavik"),
        Later(0x0f, "es", "Іспанія", "EUR", "Europe/Madrid"),
        Later(0x10, "it", "Італія", "EUR", "Europe/Rome"),
        Later(0x11, "cn", "Китай", "CNY", "Asia/Shanghai"),
        Later(0x12, "cy", "Кіпр", "EUR", "Asia/Nicosia"),
        Later(0x13, "lv", "Латвія", "EUR", "Europe/Riga"),
        Later(0x14, "lt", "Литва", "EUR", "Europe/Vilnius"),
        Later(0x15, "nl", "Нідерланди", "EUR", "Europe/Amsterdam"),
        Later(0x16, "pe", "Перу", "PEN", "America/Lima"),
        Later(0x17, "sk", "Словаччина", "EUR", "Europe/Bratislava"),
        Later(0x18, "fi", "Фінляндія", "EUR", "Europe/Helsinki"),
        Later(0x19, "fr", "Франція", "EUR", "Europe/Paris"),
        Later(0x1a, "ch", "Швейцарія", "CHF", "Europe/Zurich"),
        Later(0x1b, "se", "Швеція", "SEK", "Europe/Stockholm"),
        Later(0x1c, "jp", "Японія", "JPY", "Asia/Tokyo"),
    ];

    // The catalogue marks вмілості only as ua or pl, so a край without its own reads Ukraine's:
    // left to the default (its own code) it would offer none.
    private static Entry Later(int index, string code, string name, string currency, string timeZone, string badgeSource = Krai.Ukraine)
    {
        var profile = $$$"""{"catalog":{"badgeSources":["{{{badgeSource}}}"]},"locale":{"currency":"{{{currency}}}","timeZone":"{{{timeZone}}}"}}""";
        return new(code, name, PlastSiteSeed, new Guid($"6f0c1a52-3d4e-4b8a-9a71-2f3c4d5e6a{index:x2}"), profile);
    }
}
