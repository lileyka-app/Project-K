using ProjectK.Common.Models.Catalog;
using ProjectK.Infrastructure.Services.Catalog;
using Xunit;

namespace ProjectK.Infrastructure.Tests.Services.Catalog;

public class CatalogMapperTests
{
    private static CatalogState Map() =>
        CatalogMapper.ToState(CatalogClient.Parse(CatalogTestData.Bytes), "https://catalog.test", DateTimeOffset.UnixEpoch);

    [Fact]
    public void Badge_KeepsTheShapeClientsRead()
    {
        var badge = Map().BadgesById["plavets"];

        Assert.Equal("Плавець", badge.Title);
        Assert.Equal("https://catalog.test/v1/images/plavets.png?v=abc", badge.ImagePath);
        Assert.Equal("Україна", badge.Country);
        Assert.Equal("Водна, Туристична", badge.Specialization);
        Assert.Equal("Затверджена", badge.Status);
        Assert.Equal(1, badge.Level);
        Assert.Equal("2024-05-01", badge.LastUpdated);
        Assert.Equal("1. Пропливти 100 м\n2. Знати правила\n\nАвтор: Ольга", badge.SeekerRequirements);
        Assert.Equal("Інструктор з плавання", badge.InstructorRequirements);
        Assert.Empty(badge.FixNotes);
    }

    [Fact]
    public void Badge_CarriesTheCardPiecesSeparately()
    {
        var badge = Map().BadgesById["plavets"];

        Assert.Equal([new BadgeRequirement("1", "Пропливти 100 м"), new BadgeRequirement("2", "Знати правила")], badge.Requirements);
        Assert.Equal("Ольга", badge.Author);
        Assert.Equal(["УПЮ"], badge.Ulady);

        var bare = Map().BadgesById["bez-kraiu"];
        Assert.Empty(bare.Requirements);
        Assert.Equal(string.Empty, bare.Author);
        Assert.Empty(bare.Ulady);
    }

    [Fact]
    public void Badge_LeavesMissingFieldsEmptyInsteadOfGuessing()
    {
        var badge = Map().BadgesById["bez-kraiu"];

        Assert.Equal(string.Empty, badge.Country);
        Assert.Equal(string.Empty, badge.Specialization);
        Assert.Equal("Апробаційна", badge.Status);
        Assert.Null(badge.Level);
        Assert.Equal("https://catalog.test/v1/images/bez-kraiu-bw.png?v=def", badge.ImagePath);
        Assert.Equal(string.Empty, badge.SeekerRequirements);
    }

    [Fact]
    public void Badges_CarryTheCatalogueKraiCode()
    {
        var state = Map();

        Assert.Equal("ua", state.BadgesById["plavets"].KraiCode);
        Assert.Equal(string.Empty, state.BadgesById["bez-kraiu"].KraiCode);
    }

    [Fact]
    public void Probes_ComeInCatalogueOrder_WithSectionPointsAsTheirPoints()
    {
        var probes = Map().Probes;

        Assert.Equal(["probe-1", "probe-1-2026", "probe-2"], probes.Select(p => p.Id));
        Assert.Equal(["a-1", "a-2", "b-1"], probes[0].Points.Select(p => p.Id));
        Assert.Equal(2, probes[0].Sections!.Count);
        Assert.Equal("Знає", probes[0].Sections![0].Points[0].Title);
        Assert.Equal(["p2-1"], probes[2].Points.Select(p => p.Id));
    }

    [Fact]
    public void Probes_CarryTheirEditionItsTitlePlaceKraiAndStatus()
    {
        var probes = Map().ProbesById;

        Assert.Null(probes["probe-1"].Edition);
        Assert.Null(probes["probe-1"].EditionTitle);
        Assert.Equal("approved", probes["probe-1"].StatusCode);
        Assert.Equal("upyu-2026", probes["probe-1-2026"].Edition);
        Assert.Equal("Оновлена проба (2026)", probes["probe-1-2026"].EditionTitle);
        Assert.Equal(1, probes["probe-1-2026"].Order);
        Assert.Equal("ua", probes["probe-1-2026"].KraiCode);
        Assert.Equal("trial", probes["probe-1-2026"].StatusCode);
    }

    [Fact]
    public void Metadata_And_AwardImages()
    {
        var state = Map();

        Assert.Equal(7, state.Revision);
        Assert.Equal(2, state.Metadata.TotalBadges);
        Assert.Equal("https://catalog.test", state.Metadata.SourceUrl);
        Assert.Equal("https://catalog.test/v1/images/a1-bw.png?v=2", state.AwardImages[1].Bw);
    }

    [Fact]
    public void Parse_RefusesAnotherSchemaVersion()
    {
        var body = System.Text.Encoding.UTF8.GetBytes("""{ "schemaVersion": 3, "revision": 1 }""");

        Assert.Throws<InvalidDataException>(() => CatalogClient.Parse(body));
    }
}
