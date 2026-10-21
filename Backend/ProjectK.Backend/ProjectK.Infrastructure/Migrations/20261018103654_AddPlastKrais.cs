using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace ProjectK.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPlastKrais : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "Krais",
                columns: new[] { "Code", "CreatedAtUtc", "Name" },
                values: new object[,]
                {
                    { "ar", new DateTime(2026, 10, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Аргентина" },
                    { "at", new DateTime(2026, 10, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Австрія" },
                    { "au", new DateTime(2026, 10, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Австралія" },
                    { "be", new DateTime(2026, 10, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Бельгія" },
                    { "ca", new DateTime(2026, 10, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Канада" },
                    { "ch", new DateTime(2026, 10, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Швейцарія" },
                    { "cn", new DateTime(2026, 10, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Китай" },
                    { "cy", new DateTime(2026, 10, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Кіпр" },
                    { "de", new DateTime(2026, 10, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Німеччина" },
                    { "dk", new DateTime(2026, 10, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Данія" },
                    { "es", new DateTime(2026, 10, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Іспанія" },
                    { "fi", new DateTime(2026, 10, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Фінляндія" },
                    { "fr", new DateTime(2026, 10, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Франція" },
                    { "gb", new DateTime(2026, 10, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Велика Британія" },
                    { "ge", new DateTime(2026, 10, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Грузія" },
                    { "is", new DateTime(2026, 10, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Ісландія" },
                    { "it", new DateTime(2026, 10, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Італія" },
                    { "jp", new DateTime(2026, 10, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Японія" },
                    { "lt", new DateTime(2026, 10, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Литва" },
                    { "lv", new DateTime(2026, 10, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Латвія" },
                    { "nl", new DateTime(2026, 10, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Нідерланди" },
                    { "pe", new DateTime(2026, 10, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Перу" },
                    { "pl", new DateTime(2026, 10, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Польща" },
                    { "se", new DateTime(2026, 10, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Швеція" },
                    { "sk", new DateTime(2026, 10, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Словаччина" },
                    { "us", new DateTime(2026, 10, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Сполучені Штати Америки" }
                });

            migrationBuilder.InsertData(
                table: "KraiProfileRevisions",
                columns: new[] { "KraiProfileRevisionKey", "CreatedAtUtc", "CreatedByUserKey", "Json", "KraiCode", "Note", "Revision", "SchemaVersion" },
                values: new object[,]
                {
                    { new Guid("6f0c1a52-3d4e-4b8a-9a71-2f3c4d5e6a03"), new DateTime(2026, 10, 18, 0, 0, 0, 0, DateTimeKind.Utc), null, "{\"catalog\":{\"badgeSources\":[\"pl\"]},\"locale\":{\"currency\":\"PLN\",\"timeZone\":\"Europe/Warsaw\"}}", "pl", "Початковий профіль", 1, 1 },
                    { new Guid("6f0c1a52-3d4e-4b8a-9a71-2f3c4d5e6a04"), new DateTime(2026, 10, 18, 0, 0, 0, 0, DateTimeKind.Utc), null, "{\"catalog\":{\"badgeSources\":[\"ua\"]},\"locale\":{\"currency\":\"AUD\",\"timeZone\":\"Australia/Sydney\"}}", "au", "Початковий профіль", 1, 1 },
                    { new Guid("6f0c1a52-3d4e-4b8a-9a71-2f3c4d5e6a05"), new DateTime(2026, 10, 18, 0, 0, 0, 0, DateTimeKind.Utc), null, "{\"catalog\":{\"badgeSources\":[\"ua\"]},\"locale\":{\"currency\":\"EUR\",\"timeZone\":\"Europe/Vienna\"}}", "at", "Початковий профіль", 1, 1 },
                    { new Guid("6f0c1a52-3d4e-4b8a-9a71-2f3c4d5e6a06"), new DateTime(2026, 10, 18, 0, 0, 0, 0, DateTimeKind.Utc), null, "{\"catalog\":{\"badgeSources\":[\"ua\"]},\"locale\":{\"currency\":\"ARS\",\"timeZone\":\"America/Argentina/Buenos_Aires\"}}", "ar", "Початковий профіль", 1, 1 },
                    { new Guid("6f0c1a52-3d4e-4b8a-9a71-2f3c4d5e6a07"), new DateTime(2026, 10, 18, 0, 0, 0, 0, DateTimeKind.Utc), null, "{\"catalog\":{\"badgeSources\":[\"ua\"]},\"locale\":{\"currency\":\"GBP\",\"timeZone\":\"Europe/London\"}}", "gb", "Початковий профіль", 1, 1 },
                    { new Guid("6f0c1a52-3d4e-4b8a-9a71-2f3c4d5e6a08"), new DateTime(2026, 10, 18, 0, 0, 0, 0, DateTimeKind.Utc), null, "{\"catalog\":{\"badgeSources\":[\"ua\"]},\"locale\":{\"currency\":\"CAD\",\"timeZone\":\"America/Toronto\"}}", "ca", "Початковий профіль", 1, 1 },
                    { new Guid("6f0c1a52-3d4e-4b8a-9a71-2f3c4d5e6a09"), new DateTime(2026, 10, 18, 0, 0, 0, 0, DateTimeKind.Utc), null, "{\"catalog\":{\"badgeSources\":[\"ua\"]},\"locale\":{\"currency\":\"EUR\",\"timeZone\":\"Europe/Berlin\"}}", "de", "Початковий профіль", 1, 1 },
                    { new Guid("6f0c1a52-3d4e-4b8a-9a71-2f3c4d5e6a0a"), new DateTime(2026, 10, 18, 0, 0, 0, 0, DateTimeKind.Utc), null, "{\"catalog\":{\"badgeSources\":[\"ua\"]},\"locale\":{\"currency\":\"USD\",\"timeZone\":\"America/New_York\"}}", "us", "Початковий профіль", 1, 1 },
                    { new Guid("6f0c1a52-3d4e-4b8a-9a71-2f3c4d5e6a0b"), new DateTime(2026, 10, 18, 0, 0, 0, 0, DateTimeKind.Utc), null, "{\"catalog\":{\"badgeSources\":[\"ua\"]},\"locale\":{\"currency\":\"EUR\",\"timeZone\":\"Europe/Brussels\"}}", "be", "Початковий профіль", 1, 1 },
                    { new Guid("6f0c1a52-3d4e-4b8a-9a71-2f3c4d5e6a0c"), new DateTime(2026, 10, 18, 0, 0, 0, 0, DateTimeKind.Utc), null, "{\"catalog\":{\"badgeSources\":[\"ua\"]},\"locale\":{\"currency\":\"GEL\",\"timeZone\":\"Asia/Tbilisi\"}}", "ge", "Початковий профіль", 1, 1 },
                    { new Guid("6f0c1a52-3d4e-4b8a-9a71-2f3c4d5e6a0d"), new DateTime(2026, 10, 18, 0, 0, 0, 0, DateTimeKind.Utc), null, "{\"catalog\":{\"badgeSources\":[\"ua\"]},\"locale\":{\"currency\":\"DKK\",\"timeZone\":\"Europe/Copenhagen\"}}", "dk", "Початковий профіль", 1, 1 },
                    { new Guid("6f0c1a52-3d4e-4b8a-9a71-2f3c4d5e6a0e"), new DateTime(2026, 10, 18, 0, 0, 0, 0, DateTimeKind.Utc), null, "{\"catalog\":{\"badgeSources\":[\"ua\"]},\"locale\":{\"currency\":\"ISK\",\"timeZone\":\"Atlantic/Reykjavik\"}}", "is", "Початковий профіль", 1, 1 },
                    { new Guid("6f0c1a52-3d4e-4b8a-9a71-2f3c4d5e6a0f"), new DateTime(2026, 10, 18, 0, 0, 0, 0, DateTimeKind.Utc), null, "{\"catalog\":{\"badgeSources\":[\"ua\"]},\"locale\":{\"currency\":\"EUR\",\"timeZone\":\"Europe/Madrid\"}}", "es", "Початковий профіль", 1, 1 },
                    { new Guid("6f0c1a52-3d4e-4b8a-9a71-2f3c4d5e6a10"), new DateTime(2026, 10, 18, 0, 0, 0, 0, DateTimeKind.Utc), null, "{\"catalog\":{\"badgeSources\":[\"ua\"]},\"locale\":{\"currency\":\"EUR\",\"timeZone\":\"Europe/Rome\"}}", "it", "Початковий профіль", 1, 1 },
                    { new Guid("6f0c1a52-3d4e-4b8a-9a71-2f3c4d5e6a11"), new DateTime(2026, 10, 18, 0, 0, 0, 0, DateTimeKind.Utc), null, "{\"catalog\":{\"badgeSources\":[\"ua\"]},\"locale\":{\"currency\":\"CNY\",\"timeZone\":\"Asia/Shanghai\"}}", "cn", "Початковий профіль", 1, 1 },
                    { new Guid("6f0c1a52-3d4e-4b8a-9a71-2f3c4d5e6a12"), new DateTime(2026, 10, 18, 0, 0, 0, 0, DateTimeKind.Utc), null, "{\"catalog\":{\"badgeSources\":[\"ua\"]},\"locale\":{\"currency\":\"EUR\",\"timeZone\":\"Asia/Nicosia\"}}", "cy", "Початковий профіль", 1, 1 },
                    { new Guid("6f0c1a52-3d4e-4b8a-9a71-2f3c4d5e6a13"), new DateTime(2026, 10, 18, 0, 0, 0, 0, DateTimeKind.Utc), null, "{\"catalog\":{\"badgeSources\":[\"ua\"]},\"locale\":{\"currency\":\"EUR\",\"timeZone\":\"Europe/Riga\"}}", "lv", "Початковий профіль", 1, 1 },
                    { new Guid("6f0c1a52-3d4e-4b8a-9a71-2f3c4d5e6a14"), new DateTime(2026, 10, 18, 0, 0, 0, 0, DateTimeKind.Utc), null, "{\"catalog\":{\"badgeSources\":[\"ua\"]},\"locale\":{\"currency\":\"EUR\",\"timeZone\":\"Europe/Vilnius\"}}", "lt", "Початковий профіль", 1, 1 },
                    { new Guid("6f0c1a52-3d4e-4b8a-9a71-2f3c4d5e6a15"), new DateTime(2026, 10, 18, 0, 0, 0, 0, DateTimeKind.Utc), null, "{\"catalog\":{\"badgeSources\":[\"ua\"]},\"locale\":{\"currency\":\"EUR\",\"timeZone\":\"Europe/Amsterdam\"}}", "nl", "Початковий профіль", 1, 1 },
                    { new Guid("6f0c1a52-3d4e-4b8a-9a71-2f3c4d5e6a16"), new DateTime(2026, 10, 18, 0, 0, 0, 0, DateTimeKind.Utc), null, "{\"catalog\":{\"badgeSources\":[\"ua\"]},\"locale\":{\"currency\":\"PEN\",\"timeZone\":\"America/Lima\"}}", "pe", "Початковий профіль", 1, 1 },
                    { new Guid("6f0c1a52-3d4e-4b8a-9a71-2f3c4d5e6a17"), new DateTime(2026, 10, 18, 0, 0, 0, 0, DateTimeKind.Utc), null, "{\"catalog\":{\"badgeSources\":[\"ua\"]},\"locale\":{\"currency\":\"EUR\",\"timeZone\":\"Europe/Bratislava\"}}", "sk", "Початковий профіль", 1, 1 },
                    { new Guid("6f0c1a52-3d4e-4b8a-9a71-2f3c4d5e6a18"), new DateTime(2026, 10, 18, 0, 0, 0, 0, DateTimeKind.Utc), null, "{\"catalog\":{\"badgeSources\":[\"ua\"]},\"locale\":{\"currency\":\"EUR\",\"timeZone\":\"Europe/Helsinki\"}}", "fi", "Початковий профіль", 1, 1 },
                    { new Guid("6f0c1a52-3d4e-4b8a-9a71-2f3c4d5e6a19"), new DateTime(2026, 10, 18, 0, 0, 0, 0, DateTimeKind.Utc), null, "{\"catalog\":{\"badgeSources\":[\"ua\"]},\"locale\":{\"currency\":\"EUR\",\"timeZone\":\"Europe/Paris\"}}", "fr", "Початковий профіль", 1, 1 },
                    { new Guid("6f0c1a52-3d4e-4b8a-9a71-2f3c4d5e6a1a"), new DateTime(2026, 10, 18, 0, 0, 0, 0, DateTimeKind.Utc), null, "{\"catalog\":{\"badgeSources\":[\"ua\"]},\"locale\":{\"currency\":\"CHF\",\"timeZone\":\"Europe/Zurich\"}}", "ch", "Початковий профіль", 1, 1 },
                    { new Guid("6f0c1a52-3d4e-4b8a-9a71-2f3c4d5e6a1b"), new DateTime(2026, 10, 18, 0, 0, 0, 0, DateTimeKind.Utc), null, "{\"catalog\":{\"badgeSources\":[\"ua\"]},\"locale\":{\"currency\":\"SEK\",\"timeZone\":\"Europe/Stockholm\"}}", "se", "Початковий профіль", 1, 1 },
                    { new Guid("6f0c1a52-3d4e-4b8a-9a71-2f3c4d5e6a1c"), new DateTime(2026, 10, 18, 0, 0, 0, 0, DateTimeKind.Utc), null, "{\"catalog\":{\"badgeSources\":[\"ua\"]},\"locale\":{\"currency\":\"JPY\",\"timeZone\":\"Asia/Tokyo\"}}", "jp", "Початковий профіль", 1, 1 }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "KraiProfileRevisions",
                keyColumn: "KraiProfileRevisionKey",
                keyValue: new Guid("6f0c1a52-3d4e-4b8a-9a71-2f3c4d5e6a03"));

            migrationBuilder.DeleteData(
                table: "KraiProfileRevisions",
                keyColumn: "KraiProfileRevisionKey",
                keyValue: new Guid("6f0c1a52-3d4e-4b8a-9a71-2f3c4d5e6a04"));

            migrationBuilder.DeleteData(
                table: "KraiProfileRevisions",
                keyColumn: "KraiProfileRevisionKey",
                keyValue: new Guid("6f0c1a52-3d4e-4b8a-9a71-2f3c4d5e6a05"));

            migrationBuilder.DeleteData(
                table: "KraiProfileRevisions",
                keyColumn: "KraiProfileRevisionKey",
                keyValue: new Guid("6f0c1a52-3d4e-4b8a-9a71-2f3c4d5e6a06"));

            migrationBuilder.DeleteData(
                table: "KraiProfileRevisions",
                keyColumn: "KraiProfileRevisionKey",
                keyValue: new Guid("6f0c1a52-3d4e-4b8a-9a71-2f3c4d5e6a07"));

            migrationBuilder.DeleteData(
                table: "KraiProfileRevisions",
                keyColumn: "KraiProfileRevisionKey",
                keyValue: new Guid("6f0c1a52-3d4e-4b8a-9a71-2f3c4d5e6a08"));

            migrationBuilder.DeleteData(
                table: "KraiProfileRevisions",
                keyColumn: "KraiProfileRevisionKey",
                keyValue: new Guid("6f0c1a52-3d4e-4b8a-9a71-2f3c4d5e6a09"));

            migrationBuilder.DeleteData(
                table: "KraiProfileRevisions",
                keyColumn: "KraiProfileRevisionKey",
                keyValue: new Guid("6f0c1a52-3d4e-4b8a-9a71-2f3c4d5e6a0a"));

            migrationBuilder.DeleteData(
                table: "KraiProfileRevisions",
                keyColumn: "KraiProfileRevisionKey",
                keyValue: new Guid("6f0c1a52-3d4e-4b8a-9a71-2f3c4d5e6a0b"));

            migrationBuilder.DeleteData(
                table: "KraiProfileRevisions",
                keyColumn: "KraiProfileRevisionKey",
                keyValue: new Guid("6f0c1a52-3d4e-4b8a-9a71-2f3c4d5e6a0c"));

            migrationBuilder.DeleteData(
                table: "KraiProfileRevisions",
                keyColumn: "KraiProfileRevisionKey",
                keyValue: new Guid("6f0c1a52-3d4e-4b8a-9a71-2f3c4d5e6a0d"));

            migrationBuilder.DeleteData(
                table: "KraiProfileRevisions",
                keyColumn: "KraiProfileRevisionKey",
                keyValue: new Guid("6f0c1a52-3d4e-4b8a-9a71-2f3c4d5e6a0e"));

            migrationBuilder.DeleteData(
                table: "KraiProfileRevisions",
                keyColumn: "KraiProfileRevisionKey",
                keyValue: new Guid("6f0c1a52-3d4e-4b8a-9a71-2f3c4d5e6a0f"));

            migrationBuilder.DeleteData(
                table: "KraiProfileRevisions",
                keyColumn: "KraiProfileRevisionKey",
                keyValue: new Guid("6f0c1a52-3d4e-4b8a-9a71-2f3c4d5e6a10"));

            migrationBuilder.DeleteData(
                table: "KraiProfileRevisions",
                keyColumn: "KraiProfileRevisionKey",
                keyValue: new Guid("6f0c1a52-3d4e-4b8a-9a71-2f3c4d5e6a11"));

            migrationBuilder.DeleteData(
                table: "KraiProfileRevisions",
                keyColumn: "KraiProfileRevisionKey",
                keyValue: new Guid("6f0c1a52-3d4e-4b8a-9a71-2f3c4d5e6a12"));

            migrationBuilder.DeleteData(
                table: "KraiProfileRevisions",
                keyColumn: "KraiProfileRevisionKey",
                keyValue: new Guid("6f0c1a52-3d4e-4b8a-9a71-2f3c4d5e6a13"));

            migrationBuilder.DeleteData(
                table: "KraiProfileRevisions",
                keyColumn: "KraiProfileRevisionKey",
                keyValue: new Guid("6f0c1a52-3d4e-4b8a-9a71-2f3c4d5e6a14"));

            migrationBuilder.DeleteData(
                table: "KraiProfileRevisions",
                keyColumn: "KraiProfileRevisionKey",
                keyValue: new Guid("6f0c1a52-3d4e-4b8a-9a71-2f3c4d5e6a15"));

            migrationBuilder.DeleteData(
                table: "KraiProfileRevisions",
                keyColumn: "KraiProfileRevisionKey",
                keyValue: new Guid("6f0c1a52-3d4e-4b8a-9a71-2f3c4d5e6a16"));

            migrationBuilder.DeleteData(
                table: "KraiProfileRevisions",
                keyColumn: "KraiProfileRevisionKey",
                keyValue: new Guid("6f0c1a52-3d4e-4b8a-9a71-2f3c4d5e6a17"));

            migrationBuilder.DeleteData(
                table: "KraiProfileRevisions",
                keyColumn: "KraiProfileRevisionKey",
                keyValue: new Guid("6f0c1a52-3d4e-4b8a-9a71-2f3c4d5e6a18"));

            migrationBuilder.DeleteData(
                table: "KraiProfileRevisions",
                keyColumn: "KraiProfileRevisionKey",
                keyValue: new Guid("6f0c1a52-3d4e-4b8a-9a71-2f3c4d5e6a19"));

            migrationBuilder.DeleteData(
                table: "KraiProfileRevisions",
                keyColumn: "KraiProfileRevisionKey",
                keyValue: new Guid("6f0c1a52-3d4e-4b8a-9a71-2f3c4d5e6a1a"));

            migrationBuilder.DeleteData(
                table: "KraiProfileRevisions",
                keyColumn: "KraiProfileRevisionKey",
                keyValue: new Guid("6f0c1a52-3d4e-4b8a-9a71-2f3c4d5e6a1b"));

            migrationBuilder.DeleteData(
                table: "KraiProfileRevisions",
                keyColumn: "KraiProfileRevisionKey",
                keyValue: new Guid("6f0c1a52-3d4e-4b8a-9a71-2f3c4d5e6a1c"));

            migrationBuilder.DeleteData(
                table: "Krais",
                keyColumn: "Code",
                keyValue: "ar");

            migrationBuilder.DeleteData(
                table: "Krais",
                keyColumn: "Code",
                keyValue: "at");

            migrationBuilder.DeleteData(
                table: "Krais",
                keyColumn: "Code",
                keyValue: "au");

            migrationBuilder.DeleteData(
                table: "Krais",
                keyColumn: "Code",
                keyValue: "be");

            migrationBuilder.DeleteData(
                table: "Krais",
                keyColumn: "Code",
                keyValue: "ca");

            migrationBuilder.DeleteData(
                table: "Krais",
                keyColumn: "Code",
                keyValue: "ch");

            migrationBuilder.DeleteData(
                table: "Krais",
                keyColumn: "Code",
                keyValue: "cn");

            migrationBuilder.DeleteData(
                table: "Krais",
                keyColumn: "Code",
                keyValue: "cy");

            migrationBuilder.DeleteData(
                table: "Krais",
                keyColumn: "Code",
                keyValue: "de");

            migrationBuilder.DeleteData(
                table: "Krais",
                keyColumn: "Code",
                keyValue: "dk");

            migrationBuilder.DeleteData(
                table: "Krais",
                keyColumn: "Code",
                keyValue: "es");

            migrationBuilder.DeleteData(
                table: "Krais",
                keyColumn: "Code",
                keyValue: "fi");

            migrationBuilder.DeleteData(
                table: "Krais",
                keyColumn: "Code",
                keyValue: "fr");

            migrationBuilder.DeleteData(
                table: "Krais",
                keyColumn: "Code",
                keyValue: "gb");

            migrationBuilder.DeleteData(
                table: "Krais",
                keyColumn: "Code",
                keyValue: "ge");

            migrationBuilder.DeleteData(
                table: "Krais",
                keyColumn: "Code",
                keyValue: "is");

            migrationBuilder.DeleteData(
                table: "Krais",
                keyColumn: "Code",
                keyValue: "it");

            migrationBuilder.DeleteData(
                table: "Krais",
                keyColumn: "Code",
                keyValue: "jp");

            migrationBuilder.DeleteData(
                table: "Krais",
                keyColumn: "Code",
                keyValue: "lt");

            migrationBuilder.DeleteData(
                table: "Krais",
                keyColumn: "Code",
                keyValue: "lv");

            migrationBuilder.DeleteData(
                table: "Krais",
                keyColumn: "Code",
                keyValue: "nl");

            migrationBuilder.DeleteData(
                table: "Krais",
                keyColumn: "Code",
                keyValue: "pe");

            migrationBuilder.DeleteData(
                table: "Krais",
                keyColumn: "Code",
                keyValue: "pl");

            migrationBuilder.DeleteData(
                table: "Krais",
                keyColumn: "Code",
                keyValue: "se");

            migrationBuilder.DeleteData(
                table: "Krais",
                keyColumn: "Code",
                keyValue: "sk");

            migrationBuilder.DeleteData(
                table: "Krais",
                keyColumn: "Code",
                keyValue: "us");
        }
    }
}
