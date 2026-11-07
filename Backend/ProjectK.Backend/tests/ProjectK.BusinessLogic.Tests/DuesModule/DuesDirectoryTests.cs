using FluentAssertions;
using Moq;
using ProjectK.BusinessLogic.Modules.DuesModule.Services;
using ProjectK.BusinessLogic.Tests.TestHelpers;
using ProjectK.Common.Entities.DuesModule;
using ProjectK.Common.Interfaces.Modules.DuesModule;
using ProjectK.Common.Interfaces.Modules.KraiModule;
using ProjectK.Common.Models.Dues;
using ProjectK.Common.Models.Enums;
using Xunit;

namespace ProjectK.BusinessLogic.Tests.DuesModule;

/// <summary>A quarter counts as paid for точкування only when the ledger says it is closed with nothing owed.</summary>
public class DuesDirectoryTests
{
    private static readonly Guid Kurin = Guid.NewGuid();
    private static readonly Guid Group = Guid.NewGuid();
    private static readonly Guid Youth = Guid.NewGuid();
    private static readonly DuesQuarter Q1 = new(2026, 1);
    private static readonly DuesQuarter Q2 = new(2026, 2);
    private static readonly DuesQuarter Q3 = new(2026, 3);

    // Now is the fourth quarter: Q1–Q3 are over.
    private readonly FixedTimeProvider _time = new(new DateTimeOffset(2026, 10, 7, 12, 0, 0, TimeSpan.Zero));
    private readonly List<DuesEntry> _entries = [];

    // A senior kurin keeps the account in its own box, with no гурток.
    private Guid? _group = Group;

    private DuesDirectory Directory()
    {
        IReadOnlyList<KurinDuesRate> kurinRates = [new KurinDuesRate { KurinKey = Kurin, FromQuarter = Q1.Index, StanytsiaFull = 240, StanytsiaReduced = 180, KurinShare = 15 }];
        IReadOnlyList<GroupDuesRate> groupRates = [new GroupDuesRate { KurinKey = Kurin, GroupKey = Group, FromQuarter = Q1.Index, GroupShare = 45 }];
        IReadOnlyList<DuesConcession> concessions = [];
        IReadOnlyList<DuesCharge> charges = new[] { Q1, Q2, Q3, new DuesQuarter(2026, 4) }
            .Select(q => new DuesCharge { KurinKey = Kurin, GroupKey = _group, MembershipKey = Youth, Quarter = q.Index })
            .ToList();
        IReadOnlyList<DuesEntry> entries = _entries;

        var dues = new Mock<IDuesUnitOfWork>();
        dues.SetupGet(d => d.KurinDuesRates).Returns(Mock.Of<IKurinDuesRateRepository>(r => r.GetForKurinAsync(Kurin, It.IsAny<CancellationToken>()) == Task.FromResult(kurinRates)));
        dues.SetupGet(d => d.GroupDuesRates).Returns(Mock.Of<IGroupDuesRateRepository>(r => r.GetForKurinAsync(Kurin, It.IsAny<CancellationToken>()) == Task.FromResult(groupRates)));
        dues.SetupGet(d => d.DuesConcessions).Returns(Mock.Of<IDuesConcessionRepository>(r => r.GetForKurinAsync(Kurin, It.IsAny<CancellationToken>()) == Task.FromResult(concessions)));
        dues.SetupGet(d => d.DuesCharges).Returns(Mock.Of<IDuesChargeRepository>(r => r.GetForKurinAsync(Kurin, It.IsAny<CancellationToken>()) == Task.FromResult(charges)));
        dues.SetupGet(d => d.DuesEntries).Returns(Mock.Of<IDuesEntryRepository>(r => r.GetForKurinAsync(Kurin, It.IsAny<CancellationToken>()) == Task.FromResult(entries)));
        IReadOnlyList<DuesConcessionGrant> grants = [];
        dues.SetupGet(d => d.DuesConcessionGrants).Returns(Mock.Of<IDuesConcessionGrantRepository>(r => r.GetForKurinAsync(Kurin, It.IsAny<CancellationToken>()) == Task.FromResult(grants)));
        return new DuesDirectory(new DuesLedgerReader(dues.Object, Mock.Of<IDuesAccrual>()), _time, Mock.Of<IKraiProfileProvider>(), Mock.Of<IDuesAccrual>());
    }

    private void Pay(decimal amount) => _entries.Add(new DuesEntry
    {
        KurinKey = Kurin, GroupKey = _group, MembershipKey = Youth, Kind = DuesEntryKind.Contribution,
        Method = DuesPaymentMethod.Cash, Amount = amount, OccurredOn = new DateOnly(2026, 2, 1)
    });

    [Fact]
    public async Task OnlyQuartersPaidInFull_AndAlreadyOver_Count()
    {
        Pay(450);

        var paid = await Directory().GetPaidQuartersAsync(Kurin);

        paid.Select(p => (p.QuarterIndex, p.LastDay)).Should().Equal((Q1.Index, new DateOnly(2026, 3, 31)));
    }

    [Fact]
    public async Task TheRunningQuarter_IsNeverPaidYet()
    {
        Pay(1200);

        var paid = await Directory().GetPaidQuartersAsync(Kurin);

        paid.Select(p => p.QuarterIndex).Should().Equal(Q1.Index, Q2.Index, Q3.Index);
    }

    [Fact]
    public async Task InASeniorKurin_TheDebtForPastQuarters_IsOwedFromThePersonalAccount()
    {
        _group = null;
        Pay(255);

        var owed = await Directory().GetOwedAsync(Kurin, [Youth]);

        // Q1 is paid; Q2 and Q3 are over and owed; the running Q4 is not counted.
        owed.Should().Be(510);
    }

    [Fact]
    public async Task NothingPaid_NothingCounts()
    {
        (await Directory().GetPaidQuartersAsync(Kurin)).Should().BeEmpty();
    }

    // Charged Q1–Q4 at 300 each (240 + 15 + 45); the running Q4 is not a debt that keeps someone.
    [Fact]
    public async Task Owed_CountsOnlyQuartersAlreadyBehind()
    {
        Pay(450);

        var owed = await Directory().GetOwedAsync(Kurin, [Youth]);

        owed.Should().Be(450);
    }

    [Fact]
    public async Task Owed_IsZero_WhenOnlyTheRunningQuarterIsUnpaid()
    {
        Pay(900);

        (await Directory().GetOwedAsync(Kurin, [Youth])).Should().Be(0);
    }

    [Fact]
    public async Task Owed_IgnoresOtherMemberships()
    {
        (await Directory().GetOwedAsync(Kurin, [Guid.NewGuid()])).Should().Be(0);
    }

    [Fact]
    public async Task HasLedger_OnlyForAMembershipWithChargesOrPayments()
    {
        var directory = Directory();

        (await directory.HasLedgerAsync(Kurin, [Youth])).Should().BeTrue();
        (await directory.HasLedgerAsync(Kurin, [Guid.NewGuid()])).Should().BeFalse();
        (await directory.HasLedgerAsync(Kurin, [])).Should().BeFalse();
    }

    [Fact]
    public async Task AGroupBox_WithADebtOrMoneyInIt_IsNotSettled()
    {
        (await Directory().GroupBoxSettledAsync(Kurin, Group)).Should().BeFalse();

        Pay(1200);
        (await Directory().GroupBoxSettledAsync(Kurin, Group)).Should().BeFalse();
    }

    [Fact]
    public async Task AGroupBox_NobodyEverPaidInto_IsSettled()
    {
        (await Directory().GroupBoxSettledAsync(Kurin, Guid.NewGuid())).Should().BeTrue();
    }
}
