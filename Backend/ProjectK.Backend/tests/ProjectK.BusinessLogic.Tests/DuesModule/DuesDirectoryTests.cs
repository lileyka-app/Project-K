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

    private DuesDirectory Directory()
    {
        IReadOnlyList<KurinDuesRate> kurinRates = [new KurinDuesRate { KurinKey = Kurin, FromQuarter = Q1.Index, StanytsiaFull = 240, StanytsiaReduced = 180, KurinShare = 15 }];
        IReadOnlyList<GroupDuesRate> groupRates = [new GroupDuesRate { KurinKey = Kurin, GroupKey = Group, FromQuarter = Q1.Index, GroupShare = 45 }];
        IReadOnlyList<DuesConcession> concessions = [];
        IReadOnlyList<DuesCharge> charges = new[] { Q1, Q2, Q3, new DuesQuarter(2026, 4) }
            .Select(q => new DuesCharge { KurinKey = Kurin, GroupKey = Group, MembershipKey = Youth, Quarter = q.Index })
            .ToList();
        IReadOnlyList<DuesEntry> entries = _entries;

        var dues = new Mock<IDuesUnitOfWork>();
        dues.SetupGet(d => d.KurinDuesRates).Returns(Mock.Of<IKurinDuesRateRepository>(r => r.GetForKurinAsync(Kurin, It.IsAny<CancellationToken>()) == Task.FromResult(kurinRates)));
        dues.SetupGet(d => d.GroupDuesRates).Returns(Mock.Of<IGroupDuesRateRepository>(r => r.GetForKurinAsync(Kurin, It.IsAny<CancellationToken>()) == Task.FromResult(groupRates)));
        dues.SetupGet(d => d.DuesConcessions).Returns(Mock.Of<IDuesConcessionRepository>(r => r.GetForKurinAsync(Kurin, It.IsAny<CancellationToken>()) == Task.FromResult(concessions)));
        dues.SetupGet(d => d.DuesCharges).Returns(Mock.Of<IDuesChargeRepository>(r => r.GetForKurinAsync(Kurin, It.IsAny<CancellationToken>()) == Task.FromResult(charges)));
        dues.SetupGet(d => d.DuesEntries).Returns(Mock.Of<IDuesEntryRepository>(r => r.GetForKurinAsync(Kurin, It.IsAny<CancellationToken>()) == Task.FromResult(entries)));
        return new DuesDirectory(new DuesLedgerReader(dues.Object, Mock.Of<IDuesAccrual>()), _time, Mock.Of<IKraiProfileProvider>());
    }

    private void Pay(decimal amount) => _entries.Add(new DuesEntry
    {
        KurinKey = Kurin, GroupKey = Group, MembershipKey = Youth, Kind = DuesEntryKind.Contribution,
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
    public async Task NothingPaid_NothingCounts()
    {
        (await Directory().GetPaidQuartersAsync(Kurin)).Should().BeEmpty();
    }
}
