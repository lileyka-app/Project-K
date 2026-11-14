using FluentAssertions;
using ProjectK.BusinessLogic.Modules.KraiModule.Services;
using ProjectK.Common.Dtos.KraiModule;
using ProjectK.Common.Rules.KraiModule;

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

        errors.Should().Contain(e => e.Contains("share the key 'dup'"));
        errors.Should().Contain(e => e.Contains("latin letters, digits and hyphens"));
        errors.Should().Contain(e => e.Contains("name of 1 to 80"));
        errors.Should().Contain(e => e.Contains("at most 100 percent"));
        errors.Should().Contain(e => e.Contains("at least one part"));
        errors.Should().Contain(e => e.Contains("more than zero"));
        errors.Should().Contain(e => e.Contains("'moon'"));
        errors.Should().Contain(e => e.Contains("'gift'"));
    }
}
