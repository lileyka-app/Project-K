using System.Text.Json;
using ProjectK.Infrastructure.DbContexts;
using Xunit;

namespace ProjectK.Infrastructure.Tests.DbContexts;

public class KraiSeedTests
{
    [Fact]
    public void EveryKrai_HasItsOwnCodeAndRevisionKey()
    {
        Assert.Equal(28, KraiSeed.All.Count);
        Assert.Equal(KraiSeed.All.Count, KraiSeed.All.Select(seed => seed.Code).Distinct().Count());
        Assert.Equal(KraiSeed.All.Count, KraiSeed.All.Select(seed => seed.RevisionKey).Distinct().Count());
        Assert.All(KraiSeed.All, seed => Assert.Matches("^[a-z]{2}$", seed.Code));
    }

    [Fact]
    public void EveryLaterKrai_HasAZoneThisMachineKnows_AndBadgesTheCatalogueMarks()
    {
        Assert.All(KraiSeed.All.Where(seed => seed.Profile != "{}"), seed =>
        {
            using var profile = JsonDocument.Parse(seed.Profile);
            var locale = profile.RootElement.GetProperty("locale");
            Assert.True(TimeZoneInfo.TryFindSystemTimeZoneById(locale.GetProperty("timeZone").GetString()!, out _), seed.Code);
            Assert.Matches("^[A-Z]{3}$", locale.GetProperty("currency").GetString());
            Assert.All(
                profile.RootElement.GetProperty("catalog").GetProperty("badgeSources").EnumerateArray(),
                source => Assert.Contains(source.GetString(), new[] { "ua", "pl" }));
        });
    }

    [Fact]
    public void Poland_ReadsItsOwnBadgesInZloty()
    {
        Assert.Equal(
            """{"catalog":{"badgeSources":["pl"]},"locale":{"currency":"PLN","timeZone":"Europe/Warsaw"}}""",
            KraiSeed.All.Single(seed => seed.Code == "pl").Profile);
    }
}
