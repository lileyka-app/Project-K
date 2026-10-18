using FluentAssertions;
using Moq;
using ProjectK.BusinessLogic.Modules.ProbesAndBadgesModule.Features.Catalog;
using ProjectK.BusinessLogic.Modules.ProbesAndBadgesModule.Services;
using ProjectK.Common.Interfaces.Modules.KraiModule;
using ProjectK.Common.Interfaces.Modules.ProbesAndBadgesModule;
using ProjectK.Common.Models.Catalog;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.KraiProfiles;
using Xunit;

namespace ProjectK.BusinessLogic.Tests.ProbesAndBadgesModule;

public class KurinCatalogTests
{
    private readonly Mock<IKraiProfileProvider> _profiles = new();
    private readonly Mock<IBadgesCatalog> _badges = new();
    private readonly KurinCatalog _catalog;
    private readonly Guid _kurinKey = Guid.NewGuid();

    public KurinCatalogTests()
    {
        _badges.Setup(c => c.GetAll()).Returns(
        [
            Badge("ua-1", "ua"),
            Badge("ua-2", "ua"),
            Badge("pl-1", "pl"),
            Badge("none", ""),
        ]);
        _catalog = new KurinCatalog(_profiles.Object, _badges.Object);
    }

    private static Badge Badge(string id, string krai) =>
        new(id, id, "", "", "", "", null, "", "", "", [], [], "", []) { KraiCode = krai };

    private void Given(IReadOnlyList<string> badgeSources, IReadOnlyList<string>? hiddenBadges = null)
    {
        _profiles.Setup(p => p.GetForKurinAsync(_kurinKey, It.IsAny<CancellationToken>())).ReturnsAsync(new KraiProfile(
            "ua", "Україна", 1,
            new KraiCatalogSettings(badgeSources, hiddenBadges ?? []),
            new KraiLocaleSettings("UAH", "Europe/Kyiv", "uk-UA"),
            KraiCardFieldsSettings.Default, KraiDuesSettings.Default));
    }

    [Fact]
    public async Task AKurin_IsOfferedOnlyTheBadgesOfItsSources()
    {
        Given(["ua"]);

        var view = await _catalog.ForAsync(_kurinKey);

        view!.Badges.Select(b => b.Id).Should().Equal("ua-1", "ua-2");
        view.OffersBadge("pl-1").Should().BeFalse();
        view.OffersBadge("none").Should().BeFalse();
    }

    [Fact]
    public async Task SeveralSources_AreAllOffered()
    {
        Given(["ua", "pl"]);

        var view = await _catalog.ForAsync(_kurinKey);

        view!.Badges.Select(b => b.Id).Should().Equal("ua-1", "ua-2", "pl-1");
    }

    [Fact]
    public async Task HiddenEntries_AreNotOffered()
    {
        Given(["ua"], hiddenBadges: ["ua-2"]);

        var view = await _catalog.ForAsync(_kurinKey);

        view!.Badges.Select(b => b.Id).Should().Equal("ua-1");
        view.OffersBadge("ua-2").Should().BeFalse();
    }

    [Fact]
    public async Task AMissingKurin_HasNoCatalog()
    {
        (await _catalog.ForAsync(Guid.NewGuid())).Should().BeNull();

        var result = await new GetKurinBadgesQueryHandler(_catalog).Handle(new GetKurinBadgesQuery(Guid.NewGuid()), default);
        result.Type.Should().Be(ResultType.NotFound);
    }
}
