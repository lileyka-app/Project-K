using System.Text;

namespace ProjectK.Infrastructure.Tests.Services.Catalog;

internal static class CatalogTestData
{
    public const string Json = """
        {
          "schemaVersion": 2,
          "revision": 7,
          "krai": null,
          "categories": [{ "id": "vodna", "title": "Водна" }, { "id": "turystychna", "title": "Туристична" }],
          "editions": [{ "id": "upyu-2026", "title": "Оновлена проба (2026)", "ulad": "upyu", "krai": "ua" }],
          "badges": [
            {
              "id": "plavets", "level": 1, "title": "Плавець", "categories": ["vodna", "turystychna"], "ulady": ["upyu"],
              "krai": "ua", "status": "approved",
              "images": { "color": "https://catalog.test/v1/images/plavets.png?v=abc", "bw": null },
              "prerequisites": [],
              "requirements": [{ "id": "1", "text": "Пропливти 100 м" }, { "id": "2", "text": "Знати правила" }],
              "instructorRequirements": "Інструктор з плавання", "author": "Ольга", "updatedOn": "2024-05-01"
            },
            {
              "id": "bez-kraiu", "title": "Без краю", "categories": [], "ulady": [], "status": "trial",
              "images": { "color": null, "bw": "https://catalog.test/v1/images/bez-kraiu-bw.png?v=def" },
              "prerequisites": [], "requirements": []
            }
          ],
          "probes": [
            {
              "id": "probe-2", "title": "Друга проба", "ulad": "upyu", "krai": "ua", "order": 2, "status": "approved",
              "images": { "color": null, "bw": null }, "sections": [], "points": [{ "id": "p2-1", "text": "Своя точка" }]
            },
            {
              "id": "probe-1", "title": "Перша проба", "ulad": "upyu", "krai": "ua", "order": 1, "status": "approved",
              "images": { "color": null, "bw": null },
              "sections": [
                { "id": "s-a", "code": "А", "title": "Обов'язки", "points": [{ "id": "a-1", "text": "Знає" }, { "id": "a-2", "text": "Вміє" }] },
                { "id": "s-b", "code": "Б", "title": "Історія", "points": [{ "id": "b-1", "text": "Розповість" }] }
              ],
              "points": []
            },
            {
              "id": "probe-1-2026", "title": "Перша проба", "ulad": "upyu", "krai": "UA", "edition": "upyu-2026", "order": 1, "status": "trial",
              "images": { "color": null, "bw": null }, "sections": [],
              "points": [{ "id": "n1-1", "text": "Знає закон", "level": "knows", "competencies": ["hromadianska"] }]
            }
          ],
          "awards": [
            {
              "id": "upyu-vidznachennia-1", "group": "upyu-vidznachennia", "title": "Перше відзначення", "ulad": "upyu", "krai": "ua",
              "level": 1, "status": "approved",
              "images": { "color": "https://catalog.test/v1/images/a1.png?v=1", "bw": "https://catalog.test/v1/images/a1-bw.png?v=2" },
              "requirements": []
            }
          ]
        }
        """;

    public static byte[] Bytes => Encoding.UTF8.GetBytes(Json);
}
