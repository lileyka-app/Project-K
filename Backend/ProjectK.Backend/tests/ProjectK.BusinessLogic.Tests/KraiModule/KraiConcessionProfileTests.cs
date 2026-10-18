using FluentAssertions;
using ProjectK.BusinessLogic.Modules.KraiModule.Services;
using ProjectK.Common.Models.KraiProfiles;
using Xunit;

namespace ProjectK.BusinessLogic.Tests.KraiModule;

public class KraiConcessionProfileTests
{
    private static KraiProfileDocument With(params KraiConcessionDocument[] concessions) =>
        KraiProfileJson.Normalize(new KraiProfileDocument { Dues = new KraiDuesDocument { Concessions = concessions } });

    [Fact]
    public void Normalizing_TrimsAndLowersTheRule_AndDropsTheValueOfAnExemption()
    {
        var document = With(new KraiConcessionDocument
        {
            Key = " free ",
            Name = " Звільнення ",
            Kind = " EXEMPT ",
            Value = 5,
            AppliesTo = [" Group ", "group", ""]
        });

        var concession = document.Dues!.Concessions!.Single();
        concession.Key.Should().Be("free");
        concession.Name.Should().Be("Звільнення");
        concession.Kind.Should().Be(KraiDues.Exempt);
        concession.Value.Should().BeNull();
        concession.AppliesTo.Should().Equal("group");
        KraiProfileJson.Validate(document).Should().BeEmpty();
    }

    [Fact]
    public void AGoodRule_SurvivesTheRoundTrip()
    {
        var document = With(new KraiConcessionDocument { Key = "sibling", Name = "Брат", Kind = "percent", Value = 20, AppliesTo = ["stanytsia"] });

        var json = KraiProfileJson.Serialize(document);

        KraiProfileJson.Parse(json).Dues!.Concessions!.Single().Value.Should().Be(20);
        json.Should().Contain("\"concessions\"");
    }

    [Fact]
    public void BadRules_AreExplained()
    {
        var errors = KraiProfileJson.Validate(With(
            new KraiConcessionDocument { Key = "Bad Key", Name = "", Kind = "percent", Value = 0, AppliesTo = [] },
            new KraiConcessionDocument { Key = "dup", Name = "Одна", Kind = "amount", Value = -5, AppliesTo = ["moon"] },
            new KraiConcessionDocument { Key = "dup", Name = "Друга", Kind = "gift", AppliesTo = ["kurin"] }));

        errors.Should().Contain(e => e.Contains("однаковий ключ «dup»"));
        errors.Should().Contain(e => e.Contains("латиниця"));
        errors.Should().Contain(e => e.Contains("від 1 до 80"));
        errors.Should().Contain(e => e.Contains("від 0 до 100"));
        errors.Should().Contain(e => e.Contains("хоч одну частину"));
        errors.Should().Contain(e => e.Contains("більшою за нуль"));
        errors.Should().Contain(e => e.Contains("«moon»"));
        errors.Should().Contain(e => e.Contains("«gift»"));
    }
}
