using ProjectK.Common.Interfaces.Modules.ProbesAndBadgesModule;
using ProjectK.Infrastructure.Services.Catalog;
using Xunit;

namespace ProjectK.Infrastructure.Tests.Services.Catalog;

public class CatalogStoreTests
{
    private static CatalogStore Loaded()
    {
        var store = new CatalogStore();
        store.Replace(CatalogMapper.ToState(CatalogClient.Parse(CatalogTestData.Bytes), "https://catalog.test", DateTimeOffset.UnixEpoch));
        return store;
    }

    [Fact]
    public void ARenamedKrai_ShowsOnItsBadges_AndMovesTheVersion()
    {
        var store = Loaded();
        var before = store.Version;

        store.Name("ua", "Пласт в Україні");

        Assert.Equal("Пласт в Україні", ((IBadgesCatalog)store).GetById("plavets")!.Country);
        Assert.True(store.Version > before);
    }

    [Fact]
    public void TheName_OutlivesTheNextFetch()
    {
        var store = Loaded();
        store.Name("ua", "Пласт в Україні");

        store.Replace(CatalogMapper.ToState(CatalogClient.Parse(CatalogTestData.Bytes), "https://catalog.test", DateTimeOffset.UnixEpoch));

        Assert.Equal("Пласт в Україні", ((IBadgesCatalog)store).GetById("plavets")!.Country);
    }

    [Fact]
    public void NamesThatChangeNothing_KeepTheVersion()
    {
        var store = Loaded();
        store.NameAll([KeyValuePair.Create("ua", "Україна")]);
        var version = store.Version;

        store.NameAll([KeyValuePair.Create("ua", "Україна")]);
        store.Name("ua", "Україна");

        Assert.Equal(version, store.Version);
    }

    [Fact]
    public void ABadgeWithoutAKrai_IsLeftAsItCame()
    {
        var store = Loaded();

        store.Name("ua", "Пласт в Україні");

        Assert.Equal(string.Empty, ((IBadgesCatalog)store).GetById("bez-kraiu")!.Country);
    }
}
