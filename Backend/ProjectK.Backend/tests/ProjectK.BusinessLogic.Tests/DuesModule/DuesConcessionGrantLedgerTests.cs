using FluentAssertions;
using ProjectK.BusinessLogic.Modules.DuesModule.Services;
using ProjectK.Common.Entities.DuesModule;
using ProjectK.Common.Models.Dues;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.KraiProfiles;
using Xunit;

namespace ProjectK.BusinessLogic.Tests.DuesModule;

/// <summary>
/// The край's own пільги. Rates as in <see cref="DuesLedgerTests"/>: 240 станиця (180 пільгова),
/// 15 курінь, 45 гурток — 300 a quarter.
/// </summary>
public class DuesConcessionGrantLedgerTests
{
    private static readonly Guid Kurin = Guid.NewGuid();
    private static readonly Guid Group = Guid.NewGuid();
    private static readonly Guid Youth = Guid.NewGuid();

    private static readonly DuesQuarter Q1 = new(2026, 1);
    private static readonly DuesQuarter Q2 = new(2026, 2);
    private static readonly DuesQuarter Q3 = new(2026, 3);

    private readonly List<KurinDuesRate> _kurinRates =
        [new() { KurinKey = Kurin, FromQuarter = Q1.Index, StanytsiaFull = 240, StanytsiaReduced = 180, KurinShare = 15 }];

    private readonly List<GroupDuesRate> _groupRates =
        [new() { KurinKey = Kurin, GroupKey = Group, FromQuarter = Q1.Index, GroupShare = 45 }];

    private readonly List<DuesConcession> _concessions = [];
    private readonly List<DuesCharge> _charges = [];
    private readonly List<DuesConcessionGrant> _grants = [];

    private DuesLedger Ledger() => new(_kurinRates, _groupRates, _concessions, _charges, [], _grants);

    private void Charge(params DuesQuarter[] quarters) =>
        _charges.AddRange(quarters.Select(q => new DuesCharge { KurinKey = Kurin, GroupKey = Group, MembershipKey = Youth, Quarter = q.Index }));

    private DuesConcessionGrant Grant(string name, string kind, decimal value, DuesParts parts, DuesQuarter from, DuesQuarter? to = null)
    {
        var grant = new DuesConcessionGrant
        {
            KurinKey = Kurin,
            MembershipKey = Youth,
            ConcessionKey = name.ToLowerInvariant(),
            Name = name,
            Kind = kind,
            Value = value,
            Parts = parts,
            FromQuarter = from.Index,
            ToQuarter = to?.Index
        };
        _grants.Add(grant);
        return grant;
    }

    private IReadOnlyList<DuesAccountQuarter> Quarters() => Ledger().Accounts().Single().Quarters;

    [Fact]
    public void Percent_LowersOnlyTheNamedParts()
    {
        Grant("Брат", KraiDues.Percent, 50, DuesParts.Stanytsia | DuesParts.Group, Q1);
        Charge(Q1);

        var quarter = Quarters().Single();

        quarter.Charged.Should().Be(new DuesAmount(120, 15, 22.5m));
        quarter.ConcessionName.Should().Be("Брат");
    }

    [Fact]
    public void Amount_IsSharedBetweenThePartsInProportion()
    {
        Grant("Знижка", KraiDues.Amount, 100, DuesParts.Stanytsia | DuesParts.Kurin | DuesParts.Group, Q1);
        Charge(Q1);

        var charged = Quarters().Single().Charged;

        charged.Total.Should().Be(200);
        charged.Should().Be(new DuesAmount(160, 10, 30));
    }

    [Fact]
    public void Amount_NeverGoesBelowZero()
    {
        Grant("Знижка", KraiDues.Amount, 1000, DuesParts.Kurin, Q1);
        Charge(Q1);

        Quarters().Single().Charged.Should().Be(new DuesAmount(240, 0, 45));
    }

    [Fact]
    public void Exempt_FreesTheWholeQuarter()
    {
        Grant("Звільнення", KraiDues.Exempt, 0, DuesParts.Stanytsia | DuesParts.Krai | DuesParts.Kurin | DuesParts.Group, Q1);
        Charge(Q1);

        Quarters().Single().Charged.Total.Should().Be(0);
    }

    [Fact]
    public void TheCheapest_OfTheKurinsPilhaAndTheKraisPilhy_Counts()
    {
        _concessions.Add(new DuesConcession { KurinKey = Kurin, MembershipKey = Youth, FromQuarter = Q1.Index, IsConcession = true });
        Grant("Мала", KraiDues.Percent, 10, DuesParts.Stanytsia, Q1);
        Grant("Велика", KraiDues.Percent, 50, DuesParts.Stanytsia, Q2);
        Charge(Q1, Q2);

        var quarters = Quarters();

        // Q1: the kurin's пільга (180) beats 10 % off (216).
        quarters[0].Charged.Should().Be(new DuesAmount(180, 15, 45));
        quarters[0].ConcessionName.Should().BeNull();
        quarters[0].IsConcession.Should().BeTrue();
        // Q2: 50 % off the full станиця part (120) beats both.
        quarters[1].Charged.Should().Be(new DuesAmount(120, 15, 45));
        quarters[1].ConcessionName.Should().Be("Велика");
    }

    [Fact]
    public void Grant_CoversOnlyItsQuarters()
    {
        Grant("Брат", KraiDues.Exempt, 0, DuesParts.Group, Q2, Q2);
        Charge(Q1, Q2, Q3);

        Quarters().Select(q => q.Charged.Group).Should().Equal(45, 0, 45);
    }

    [Fact]
    public void Priced_KeepsThePriceWithoutTheKraisPilhy_ForTheReport()
    {
        _concessions.Add(new DuesConcession { KurinKey = Kurin, MembershipKey = Youth, FromQuarter = Q1.Index, IsConcession = true });
        var grant = Grant("Звільнення", KraiDues.Exempt, 0, DuesParts.Stanytsia, Q1);
        Charge(Q1);

        var price = Ledger().Priced(Youth, Group, Q1);

        price.Grant.Should().BeSameAs(grant);
        price.WithoutGrants.Should().Be(new DuesAmount(180, 15, 45));
        price.Amount.Should().Be(new DuesAmount(0, 15, 45));
    }

    [Fact]
    public void Rule_RoundsToCents_AndTheCentsAddUp()
    {
        var rule = new DuesConcessionRule("x", "x", KraiDues.Amount, 10, DuesParts.Stanytsia | DuesParts.Kurin | DuesParts.Group);

        var result = rule.Apply(new DuesAmount(1, 1, 1));

        (3 - result.Total).Should().Be(3);
        result.Should().Be(new DuesAmount(0, 0, 0));

        var third = new DuesConcessionRule("y", "y", KraiDues.Amount, 1, DuesParts.Stanytsia | DuesParts.Kurin | DuesParts.Group)
            .Apply(new DuesAmount(10, 10, 10));
        third.Total.Should().Be(29);
    }
}
