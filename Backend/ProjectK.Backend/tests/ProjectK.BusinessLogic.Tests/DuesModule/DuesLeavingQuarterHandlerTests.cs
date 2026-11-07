using Moq;
using ProjectK.BusinessLogic.Modules.DuesModule.Services;
using ProjectK.BusinessLogic.Services.Events;
using ProjectK.Common.Entities.DuesModule;
using ProjectK.Common.Interfaces.Modules.DuesModule;
using ProjectK.Common.Interfaces.Modules.KraiModule;
using ProjectK.Common.Models.Dues;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Events;
using Xunit;

namespace ProjectK.BusinessLogic.Tests.DuesModule;

/// <summary>
/// The quarter a person is released in: paid in full, it stays theirs and on the record; not paid,
/// they do not take it and its charge goes. Every quarter before it stays either way.
/// </summary>
public class DuesLeavingQuarterHandlerTests
{
    private static readonly Guid Kurin = Guid.NewGuid();
    private static readonly Guid Group = Guid.NewGuid();
    private static readonly Guid Youth = Guid.NewGuid();
    private static readonly DuesQuarter Q3 = new(2026, 3);
    private static readonly DuesQuarter Q4 = new(2026, 4);

    // No край profile: Europe/Kyiv. 2 October 2026 is in the fourth quarter.
    private static readonly DateTime LeftAt = new(2026, 10, 2, 9, 0, 0, DateTimeKind.Utc);

    private readonly List<DuesEntry> _entries = [];
    private readonly Mock<IDuesChargeRepository> _charges = new();

    private DuesLeavingQuarterEventHandler Handler()
    {
        IReadOnlyList<KurinDuesRate> kurinRates = [new KurinDuesRate { KurinKey = Kurin, FromQuarter = Q3.Index, StanytsiaFull = 240, StanytsiaReduced = 180, KurinShare = 15 }];
        IReadOnlyList<GroupDuesRate> groupRates = [new GroupDuesRate { KurinKey = Kurin, GroupKey = Group, FromQuarter = Q3.Index, GroupShare = 45 }];
        IReadOnlyList<DuesConcession> concessions = [];
        IReadOnlyList<DuesConcessionGrant> grants = [];
        IReadOnlyList<DuesCharge> charges = new[] { Q3, Q4 }
            .Select(q => new DuesCharge { KurinKey = Kurin, GroupKey = Group, MembershipKey = Youth, Quarter = q.Index })
            .ToList();
        IReadOnlyList<DuesEntry> entries = _entries;
        _charges.Setup(r => r.GetForKurinAsync(Kurin, It.IsAny<CancellationToken>())).ReturnsAsync(charges);

        var dues = new Mock<IDuesUnitOfWork>();
        dues.SetupGet(d => d.KurinDuesRates).Returns(Mock.Of<IKurinDuesRateRepository>(r => r.GetForKurinAsync(Kurin, It.IsAny<CancellationToken>()) == Task.FromResult(kurinRates)));
        dues.SetupGet(d => d.GroupDuesRates).Returns(Mock.Of<IGroupDuesRateRepository>(r => r.GetForKurinAsync(Kurin, It.IsAny<CancellationToken>()) == Task.FromResult(groupRates)));
        dues.SetupGet(d => d.DuesConcessions).Returns(Mock.Of<IDuesConcessionRepository>(r => r.GetForKurinAsync(Kurin, It.IsAny<CancellationToken>()) == Task.FromResult(concessions)));
        dues.SetupGet(d => d.DuesConcessionGrants).Returns(Mock.Of<IDuesConcessionGrantRepository>(r => r.GetForKurinAsync(Kurin, It.IsAny<CancellationToken>()) == Task.FromResult(grants)));
        dues.SetupGet(d => d.DuesEntries).Returns(Mock.Of<IDuesEntryRepository>(r => r.GetForKurinAsync(Kurin, It.IsAny<CancellationToken>()) == Task.FromResult(entries)));
        dues.SetupGet(d => d.DuesCharges).Returns(_charges.Object);

        return new DuesLeavingQuarterEventHandler(dues.Object, new DuesLedgerReader(dues.Object, Mock.Of<IDuesAccrual>()), Mock.Of<IKraiProfileProvider>());
    }

    private void Pay(decimal amount) => _entries.Add(new DuesEntry
    {
        KurinKey = Kurin, GroupKey = Group, MembershipKey = Youth, Kind = DuesEntryKind.Contribution,
        Method = DuesPaymentMethod.Cash, Amount = amount, OccurredOn = new DateOnly(2026, 9, 1)
    });

    private Task Leave() => Handler().Handle(
        new DomainEventNotification<MembershipLeft>(new MembershipLeft(Youth, Kurin, LeftAt)), CancellationToken.None);

    [Fact]
    public async Task AnUnpaidLeavingQuarter_IsDropped()
    {
        Pay(300);

        await Leave();

        _charges.Verify(c => c.DeleteFromQuarterAsync(Youth, Q4.Index, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task APaidLeavingQuarter_StaysOnTheRecord()
    {
        Pay(600);

        await Leave();

        _charges.Verify(c => c.DeleteFromQuarterAsync(It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
