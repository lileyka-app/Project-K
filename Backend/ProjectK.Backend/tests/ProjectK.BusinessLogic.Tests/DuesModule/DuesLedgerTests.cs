using FluentAssertions;
using ProjectK.BusinessLogic.Modules.DuesModule.Services;
using ProjectK.Common.Entities.DuesModule;
using ProjectK.Common.Models.Dues;
using ProjectK.Common.Models.Enums;
using Xunit;

namespace ProjectK.BusinessLogic.Tests.DuesModule;

/// <summary>
/// The money rules of the вкладка, one by one. Rates are the ones the гурток uses
/// today: 240 станиця (180 пільгова), 15 курінь, 45 гурток — 300 a quarter.
/// </summary>
public class DuesLedgerTests
{
    private static readonly Guid Kurin = Guid.NewGuid();
    private static readonly Guid GroupA = Guid.NewGuid();
    private static readonly Guid GroupB = Guid.NewGuid();
    private static readonly Guid Youth = Guid.NewGuid();

    private static readonly DuesQuarter Q1 = new(2026, 1);
    private static readonly DuesQuarter Q2 = new(2026, 2);
    private static readonly DuesQuarter Q3 = new(2026, 3);

    private readonly List<KurinDuesRate> _kurinRates =
        [new() { KurinKey = Kurin, FromQuarter = Q1.Index, StanytsiaFull = 240, StanytsiaReduced = 180, KurinShare = 15 }];

    private readonly List<GroupDuesRate> _groupRates =
    [
        new() { KurinKey = Kurin, GroupKey = GroupA, FromQuarter = Q1.Index, GroupShare = 45 },
        new() { KurinKey = Kurin, GroupKey = GroupB, FromQuarter = Q1.Index, GroupShare = 60 }
    ];

    private readonly List<DuesConcession> _concessions = [];
    private readonly List<DuesCharge> _charges = [];
    private readonly List<DuesEntry> _entries = [];

    private DuesLedger Ledger() => new(_kurinRates, _groupRates, _concessions, _charges, _entries);

    private void Charge(Guid group, params DuesQuarter[] quarters) =>
        _charges.AddRange(quarters.Select(q => new DuesCharge { KurinKey = Kurin, GroupKey = group, MembershipKey = Youth, Quarter = q.Index }));

    private DuesEntry Entry(DuesEntryKind kind, decimal amount, Guid? group = null, DuesPaymentMethod method = DuesPaymentMethod.Cash, Guid? membership = null)
    {
        var entry = new DuesEntry
        {
            KurinKey = Kurin,
            GroupKey = group,
            MembershipKey = membership,
            Kind = kind,
            Amount = amount,
            Method = method,
            OccurredOn = new DateOnly(2026, 2, 1)
        };
        _entries.Add(entry);
        return entry;
    }

    private DuesEntry Pay(decimal amount, Guid? group = null, DuesPaymentMethod method = DuesPaymentMethod.Cash) =>
        Entry(DuesEntryKind.Contribution, amount, group ?? GroupA, method, Youth);

    private DuesAccount Account(Guid group) => Ledger().Accounts().Single(a => a.GroupKey == group);

    [Fact]
    public void FullPayment_ClosesTheQuarter()
    {
        Charge(GroupA, Q1);
        Pay(300);

        var quarter = Account(GroupA).Quarters.Single();

        quarter.Charged.Total.Should().Be(300);
        quarter.Paid.Should().Be(new DuesAmount(240, 15, 45));
        quarter.Balance.Should().Be(0);
    }

    [Fact]
    public void PartialPayment_PaysTheStanytsiaAndTheKurinBeforeTheGroup()
    {
        Charge(GroupA, Q1);
        Pay(150);

        Account(GroupA).Quarters.Single().Paid.Should().Be(new DuesAmount(150, 0, 0));

        Pay(100);

        Account(GroupA).Quarters.Single().Paid.Should().Be(new DuesAmount(240, 10, 0));
    }

    [Fact]
    public void Payment_ClosesTheOldestQuarterFirst()
    {
        Charge(GroupA, Q1, Q2);
        Pay(400);

        var account = Account(GroupA);

        account.Quarters[0].Balance.Should().Be(0);
        account.Quarters[1].Paid.Should().Be(new DuesAmount(100, 0, 0));
        account.Balance.Should().Be(-200);
    }

    [Fact]
    public void Surplus_IsTheAccountsAndPaysTheNextQuarterWhenItIsCharged()
    {
        Charge(GroupA, Q1);
        Pay(400);

        Account(GroupA).Balance.Should().Be(100);

        Charge(GroupA, Q2);

        var account = Account(GroupA);
        account.Quarters[1].Paid.Should().Be(new DuesAmount(100, 0, 0));
        account.Balance.Should().Be(-200);
    }

    [Fact]
    public void Concession_LowersOnlyTheStanytsiaPart_FromItsQuarter()
    {
        _concessions.Add(new DuesConcession { KurinKey = Kurin, MembershipKey = Youth, FromQuarter = Q2.Index, IsConcession = true });
        Charge(GroupA, Q1, Q2);

        var quarters = Account(GroupA).Quarters;

        quarters[0].Charged.Should().Be(new DuesAmount(240, 15, 45));
        quarters[1].Charged.Should().Be(new DuesAmount(180, 15, 45));
        quarters[1].IsConcession.Should().BeTrue();
    }

    [Fact]
    public void Concession_TakesThePilhovyAmountOfEveryStattia()
    {
        var rate = new KurinDuesRate { KurinKey = Kurin, FromQuarter = Q2.Index };
        rate.SetArticles(
        [
            new KurinDuesArticle { Name = "Станиця", Recipient = DuesRecipient.Stanytsia, Amount = 240, ReducedAmount = 180 },
            new KurinDuesArticle { Name = "Курінь", Recipient = DuesRecipient.Kurin, Amount = 15, ReducedAmount = 15 },
            new KurinDuesArticle { Name = "Табірний фонд", Recipient = DuesRecipient.Kurin, Amount = 50, ReducedAmount = 25 }
        ]);
        _kurinRates.Add(rate);
        _concessions.Add(new DuesConcession { KurinKey = Kurin, MembershipKey = Youth, FromQuarter = Q2.Index, IsConcession = true });
        Charge(GroupA, Q1, Q2);

        var quarters = Account(GroupA).Quarters;

        quarters[0].Charged.Should().Be(new DuesAmount(240, 15, 45));
        quarters[1].Charged.Should().Be(new DuesAmount(180, 40, 45));
    }

    [Fact]
    public void RateChange_AppliesFromItsQuarter()
    {
        _kurinRates.Add(new KurinDuesRate { KurinKey = Kurin, FromQuarter = Q3.Index, StanytsiaFull = 300, StanytsiaReduced = 200, KurinShare = 20 });
        Charge(GroupA, Q2, Q3);

        var quarters = Account(GroupA).Quarters;

        quarters[0].Charged.Total.Should().Be(300);
        quarters[1].Charged.Total.Should().Be(365);
    }

    [Fact]
    public void AfterAMove_TheOldDebtAndItsGroupPartStayWithTheOldGroup()
    {
        Charge(GroupA, Q1);
        Charge(GroupB, Q2);
        Pay(300, GroupB);

        var accounts = Ledger().Accounts();

        accounts.Single(a => a.GroupKey == GroupA).Balance.Should().Be(-300);
        accounts.Single(a => a.GroupKey == GroupB).Quarters.Single().Paid.Should().Be(new DuesAmount(240, 15, 45));
    }

    [Fact]
    public void RefundAndCorrection_MoveTheBalance()
    {
        Charge(GroupA, Q1);
        Pay(400);
        Entry(DuesEntryKind.Refund, 100, GroupA, membership: Youth);

        Account(GroupA).Balance.Should().Be(0);

        Entry(DuesEntryKind.Correction, -50, GroupA, membership: Youth);

        Account(GroupA).Balance.Should().Be(-50);
    }

    [Fact]
    public void DeletedEntries_DoNotCount()
    {
        Charge(GroupA, Q1);
        Pay(300).DeletedAtUtc = DateTime.UtcNow;

        Account(GroupA).Balance.Should().Be(-300);
    }

    [Fact]
    public void GroupBox_SplitsCashAndCard_AndOwnMoneyFromWhatGoesUp()
    {
        Charge(GroupA, Q1);
        Pay(200, method: DuesPaymentMethod.Cash);
        Pay(100, method: DuesPaymentMethod.Card);
        Entry(DuesEntryKind.Expense, 20, GroupA, DuesPaymentMethod.Cash);

        var box = Ledger().GroupBox(GroupA).Box;

        box.Cash.Should().Be(180);
        box.Card.Should().Be(100);
        box.ToForward.Should().Be(255);
        box.Own.Should().Be(25);
    }

    [Fact]
    public void Exchange_MovesMoneyBetweenCashAndCard_WithoutChangingTheTotal()
    {
        Charge(GroupA, Q1);
        Pay(300);
        Entry(DuesEntryKind.Exchange, 200, GroupA, DuesPaymentMethod.Cash).CounterMethod = DuesPaymentMethod.Card;

        var box = Ledger().GroupBox(GroupA).Box;

        box.Cash.Should().Be(100);
        box.Card.Should().Be(200);
        box.Total.Should().Be(300);
    }

    [Fact]
    public void TransferToKurin_LeavesTheGroupAtOnce_AndEntersTheKurinOnlyWhenConfirmed()
    {
        Charge(GroupA, Q1);
        Pay(300);
        var transfer = Entry(DuesEntryKind.TransferToKurin, 255, GroupA);

        var groupBox = Ledger().GroupBox(GroupA);
        groupBox.Box.Total.Should().Be(45);
        groupBox.Box.ToForward.Should().Be(0);
        groupBox.Box.Own.Should().Be(45);
        groupBox.InTransit.Should().Be(255);
        Ledger().KurinBox().Box.Total.Should().Be(0);

        transfer.ReceivedAtUtc = DateTime.UtcNow;

        var kurinBox = Ledger().KurinBox();
        kurinBox.Box.Total.Should().Be(255);
        kurinBox.Box.ToForward.Should().Be(240);
        kurinBox.Box.Own.Should().Be(15);
        kurinBox.Handovers.Single().Should().Be(new GroupHandover(GroupA, 255, 255, 255));
    }

    [Fact]
    public void KurinBox_CountsAPartialHandoverAsStanytsiaMoneyFirst()
    {
        Charge(GroupA, Q1);
        Pay(300);
        Entry(DuesEntryKind.TransferToKurin, 100, GroupA).ReceivedAtUtc = DateTime.UtcNow;

        var kurinBox = Ledger().KurinBox();

        kurinBox.Box.ToForward.Should().Be(100);
        kurinBox.Box.Own.Should().Be(0);
        kurinBox.Handovers.Single().Outstanding.Should().Be(155);
    }

    [Fact]
    public void KurinBox_HandingToTheStanytsiaLowersWhatIsOwedThere()
    {
        Charge(GroupA, Q1);
        Pay(300);
        Entry(DuesEntryKind.TransferToKurin, 255, GroupA).ReceivedAtUtc = DateTime.UtcNow;
        Entry(DuesEntryKind.TransferToStanytsia, 240, null, DuesPaymentMethod.Card);

        var box = Ledger().KurinBox().Box;

        box.Cash.Should().Be(255);
        box.Card.Should().Be(-240);
        box.ToForward.Should().Be(0);
        box.Own.Should().Be(15);
    }

    private void KraiArticleFromQ1() =>
        _kurinRates[0].SetArticles(
        [
            new KurinDuesArticle { Name = "Станиця", Recipient = DuesRecipient.Stanytsia, Amount = 240, ReducedAmount = 180 },
            new KurinDuesArticle { Name = "Крайова вкладка", Recipient = DuesRecipient.Krai, Amount = 60, ReducedAmount = 30 },
            new KurinDuesArticle { Name = "Курінь", Recipient = DuesRecipient.Kurin, Amount = 15, ReducedAmount = 15 }
        ]);

    [Fact]
    public void AKraiArticle_IsPaidAfterTheStanytsia_AndBeforeTheKurin()
    {
        KraiArticleFromQ1();
        Charge(GroupA, Q1);
        Pay(270);

        var quarter = Account(GroupA).Quarters.Single();

        quarter.Charged.Should().Be(new DuesAmount(240, 15, 45, 60));
        quarter.Paid.Should().Be(new DuesAmount(240, 0, 0, 30));
    }

    [Fact]
    public void AKraiArticle_TakesItsPilhaAmount_WithAPilha()
    {
        KraiArticleFromQ1();
        Charge(GroupA, Q1);
        _concessions.Add(new DuesConcession { KurinKey = Kurin, MembershipKey = Youth, FromQuarter = Q1.Index, IsConcession = true });

        Account(GroupA).Quarters.Single().Charged.Should().Be(new DuesAmount(180, 15, 45, 30));
    }

    [Fact]
    public void KurinBox_KeepsTheKraisMoneyApartFromTheStanytsias_UntilEachIsHandedOn()
    {
        KraiArticleFromQ1();
        Charge(GroupA, Q1);
        Pay(360);
        Entry(DuesEntryKind.TransferToKurin, 315, GroupA).ReceivedAtUtc = DateTime.UtcNow;

        var before = Ledger().KurinBox();
        before.ToStanytsia.Should().Be(240);
        before.ToKrai.Should().Be(60);
        before.Box.ToForward.Should().Be(300);
        before.Handovers.Single().OwedUp.Should().Be(315);

        Entry(DuesEntryKind.TransferToKrai, 60, null, DuesPaymentMethod.Card);

        var after = Ledger().KurinBox();
        after.ToStanytsia.Should().Be(240);
        after.ToKrai.Should().Be(0);
        after.Box.Card.Should().Be(-60);
        after.Box.Own.Should().Be(15);
    }

    [Fact]
    public void KurinBox_CountsAPartialHandoverAsStanytsiaMoney_ThenKraiMoney()
    {
        KraiArticleFromQ1();
        Charge(GroupA, Q1);
        Pay(360);
        Entry(DuesEntryKind.TransferToKurin, 270, GroupA).ReceivedAtUtc = DateTime.UtcNow;

        var box = Ledger().KurinBox();

        box.ToStanytsia.Should().Be(240);
        box.ToKrai.Should().Be(30);
        box.Box.Own.Should().Be(0);
    }
}
