using ProjectK.Infrastructure.BackgroundServices;
using ProjectK.Common.Dtos.ProbesAndBadgesModule;
using ProjectK.Common.Enums.ProbesAndBadgesModule;
using ProjectK.Common.Enums.Shared;
using ProjectK.Common.Rules.Shared;
using ProjectK.Infrastructure.Services.Catalog;

namespace ProjectK.Infrastructure.Tests.Services.Catalog;

/// <summary>
/// Every ступінь a card can hold has its відзнака in the catalogue Lilejka ships with, a пластун's and
/// a пластунка's apart; only the day of joining has none.
/// </summary>
public class PlastLevelDegreeRulesTests
{
    private static IReadOnlyDictionary<string, CatalogHonour> SeedDegrees()
    {
        using var stream = typeof(CatalogSyncBackgroundService).Assembly.GetManifestResourceStream(CatalogSyncBackgroundService.SeedResourceName)!;
        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);
        return CatalogMapper.ToState(CatalogClient.Parse(buffer.ToArray()), "seed", null).HonoursById;
    }

    [Fact]
    public void EveryStupin_IsInTheCatalogue_ForAPlastunAndAPlastunka()
    {
        var degrees = SeedDegrees();

        foreach (var level in Enum.GetValues<PlastLevel>().Where(level => level != PlastLevel.Entry))
        {
            foreach (var plastunka in new[] { false, true })
            {
                var id = PlastLevelDegreeRules.CatalogId(level, plastunka);
                Assert.NotNull(id);
                Assert.True(degrees.TryGetValue(id!, out var degree), $"{level} ({(plastunka ? "пластунка" : "пластун")}): {id} is not in the catalogue");
                Assert.Equal(HonourKind.Degree, degree!.Kind);
            }
        }

        Assert.Null(PlastLevelDegreeRules.CatalogId(PlastLevel.Entry, false));
        Assert.Equal("usp-starsha-plastunka", PlastLevelDegreeRules.CatalogId(PlastLevel.Starshoplastun, true));
    }
}
