using FluentAssertions;
using Moq;
using ProjectK.BusinessLogic.Modules.DuesModule.Events;
using ProjectK.BusinessLogic.Modules.DuesModule.Services;
using ProjectK.BusinessLogic.Services.Events;
using ProjectK.BusinessLogic.Tests.TestHelpers;
using ProjectK.Common.Entities.DuesModule;
using ProjectK.Common.Interfaces.Modules.DuesModule;
using ProjectK.Common.Interfaces.Modules.KraiModule;
using ProjectK.Common.Interfaces.Modules.KurinModule;
using ProjectK.Common.Models.Dues;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Events;
using ProjectK.Common.Models.KraiProfiles;
using ProjectK.Common.Models.Records;
using Xunit;

namespace ProjectK.BusinessLogic.Tests.DuesModule;

public class DuesAccrualTests
{
    private static readonly Guid Kurin = Guid.NewGuid();
    private static readonly Guid GroupA = Guid.NewGuid();
    private static readonly Guid GroupB = Guid.NewGuid();

    // Now is the second quarter of 2026.
    private FixedTimeProvider _time = new(new DateTimeOffset(2026, 5, 10, 12, 0, 0, TimeSpan.Zero));

    // No profile: the instance's zone, Europe/Kyiv.
    private string? _zone;

    private readonly List<KurinDuesRate> _rates = [];
    private readonly List<DuesCharge> _existing = [];
    private readonly List<DuesCharge> _created = [];
    private readonly List<KurinMembershipRecord> _memberships = [];
    private readonly List<DuesEntry> _entries = [];
    private readonly List<DuesCharge> _elsewhere = [];
    private readonly List<MembershipRecord> _history = [];
    private readonly Mock<IDuesUnitOfWork> _dues = new();
    private readonly Mock<IDuesKurinShape> _shape = new();

    // No row: the kurin pays наперед.
    private KurinDuesSettings? _settings;

    public DuesAccrualTests()
    {
        var rates = new Mock<IKurinDuesRateRepository>();
        rates.Setup(r => r.GetForKurinAsync(Kurin, It.IsAny<CancellationToken>())).ReturnsAsync(() => _rates);
        var charges = new Mock<IDuesChargeRepository>();
        charges.Setup(r => r.GetForKurinAsync(Kurin, It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => _existing.Concat(_created).ToList());
        charges.Setup(r => r.Create(It.IsAny<DuesCharge>(), It.IsAny<CancellationToken>()))
            .Callback<DuesCharge, CancellationToken>((charge, _) => _created.Add(charge));
        charges.Setup(r => r.GetTrackedFromQuarterAsync(Kurin, It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid _, int from, CancellationToken _) => _existing.Concat(_created).Where(c => c.Quarter >= from).ToList());
        charges.Setup(r => r.GetForMembershipsAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyCollection<Guid> keys, CancellationToken _) => _elsewhere.Where(c => keys.Contains(c.MembershipKey)).ToList());
        _dues.SetupGet(d => d.KurinDuesRates).Returns(rates.Object);
        _dues.SetupGet(d => d.DuesCharges).Returns(charges.Object);
        var entries = new Mock<IDuesEntryRepository>();
        entries.Setup(r => r.GetForKurinAsync(Kurin, It.IsAny<CancellationToken>())).ReturnsAsync(() => _entries);
        _dues.SetupGet(d => d.DuesEntries).Returns(entries.Object);
        _dues.SetupGet(d => d.GroupDuesRates).Returns(Mock.Of<IGroupDuesRateRepository>(r => r.GetForKurinAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()) == Task.FromResult<IReadOnlyList<GroupDuesRate>>(Array.Empty<GroupDuesRate>())));
        _dues.SetupGet(d => d.DuesConcessions).Returns(Mock.Of<IDuesConcessionRepository>(r => r.GetForKurinAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()) == Task.FromResult<IReadOnlyList<DuesConcession>>(Array.Empty<DuesConcession>())));
        _dues.SetupGet(d => d.DuesConcessionGrants).Returns(Mock.Of<IDuesConcessionGrantRepository>(r => r.GetForKurinAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()) == Task.FromResult<IReadOnlyList<DuesConcessionGrant>>(Array.Empty<DuesConcessionGrant>())));
        var settings = new Mock<IKurinDuesSettingsRepository>();
        settings.Setup(r => r.GetForKurinAsync(Kurin, It.IsAny<CancellationToken>())).ReturnsAsync(() => _settings);
        _dues.SetupGet(d => d.KurinDuesSettings).Returns(settings.Object);
    }

    private DuesAccrual Accrual()
    {
        var directory = new Mock<IMembershipDirectory>();
        directory.Setup(d => d.GetInKurinAsync(Kurin, It.IsAny<CancellationToken>())).ReturnsAsync(() => _memberships);
        directory.Setup(d => d.GetForMembersAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyCollection<Guid> keys, CancellationToken _) =>
                keys.ToDictionary(key => key, key => (IReadOnlyCollection<MembershipRecord>)_history));
        var profiles = new Mock<IKraiProfileProvider>();
        if (_zone is not null)
        {
            profiles.Setup(p => p.GetForKurinAsync(Kurin, It.IsAny<CancellationToken>())).ReturnsAsync(new KraiProfile(
                "ca", "Канада", 1, new KraiCatalogSettings(["ca"], []), new KraiLocaleSettings("CAD", _zone, "en-CA"), KraiCardFieldsSettings.Default, KraiDuesSettings.Default));
        }

        return new DuesAccrual(_dues.Object, directory.Object, _time, profiles.Object, _shape.Object);
    }

    private void RateFrom(DuesQuarter quarter) =>
        _rates.Add(new KurinDuesRate { KurinKey = Kurin, FromQuarter = quarter.Index, StanytsiaFull = 240, KurinShare = 15 });

    private KurinMembershipRecord Member(Guid? group, DateTime joined, MembershipKind kind = MembershipKind.Youth, DateTime? left = null)
    {
        var record = new KurinMembershipRecord(Guid.NewGuid(), Guid.NewGuid(), group, kind, joined, left);
        _memberships.Add(record);
        return record;
    }

    private IEnumerable<DuesQuarter> QuartersOf(KurinMembershipRecord membership) =>
        _created.Where(c => c.MembershipKey == membership.MembershipKey).Select(c => DuesQuarter.FromIndex(c.Quarter));

    private void Senior(params (Guid Member, Guid[] Tags)[] people)
    {
        _shape.Setup(s => s.IsSeniorAsync(Kurin, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _shape.Setup(s => s.TagsByMemberAsync(Kurin, It.IsAny<CancellationToken>()))
            .ReturnsAsync(people.ToDictionary(p => p.Member, p => (IReadOnlyCollection<Guid>)p.Tags));
    }

    [Fact]
    public async Task UpyuKurin_DoesNotChargeSomeoneOutsideAGroup()
    {
        RateFrom(new DuesQuarter(2026, 1));
        Member(null, new DateTime(2025, 1, 1));

        await Accrual().AccrueKurinAsync(Kurin);

        _created.Should().BeEmpty();
    }

    [Fact]
    public async Task SeniorKurin_ChargesEveryMemberInTheKurinsOwnBox_ProvidToo()
    {
        RateFrom(new DuesQuarter(2026, 1));
        var member = Member(null, new DateTime(2025, 1, 1));
        var provid = Member(null, new DateTime(2025, 1, 1), MembershipKind.Staff);
        Senior();

        await Accrual().AccrueKurinAsync(Kurin);

        QuartersOf(member).Should().Equal(new DuesQuarter(2026, 1), new DuesQuarter(2026, 2));
        QuartersOf(provid).Should().Equal(new DuesQuarter(2026, 1), new DuesQuarter(2026, 2));
        _created.Should().OnlyContain(c => c.GroupKey == null && c.RateTagKey == null);
    }

    [Fact]
    public async Task SeniorKurin_PricesAQuarterAtTheHighestRateAmongThePersonsTags()
    {
        var usp = Guid.NewGuid();
        var ups = Guid.NewGuid();
        var other = Guid.NewGuid();
        RateFrom(new DuesQuarter(2026, 1));
        _rates.Add(new KurinDuesRate { KurinKey = Kurin, TagKey = usp, FromQuarter = new DuesQuarter(2026, 1).Index, KurinShare = 300 });
        // The УПС rate is higher, but only from the second quarter.
        _rates.Add(new KurinDuesRate { KurinKey = Kurin, TagKey = ups, FromQuarter = new DuesQuarter(2026, 2).Index, KurinShare = 450 });
        var both = Member(null, new DateTime(2025, 1, 1));
        var untagged = Member(null, new DateTime(2025, 1, 1));
        Senior((both.MemberKey, [usp, ups]), (untagged.MemberKey, [other]));

        await Accrual().AccrueKurinAsync(Kurin);

        _created.Where(c => c.MembershipKey == both.MembershipKey).OrderBy(c => c.Quarter).Select(c => c.RateTagKey)
            .Should().Equal(usp, ups);
        _created.Where(c => c.MembershipKey == untagged.MembershipKey).Should().OnlyContain(c => c.RateTagKey == null);
    }

    [Fact]
    public async Task WithoutAKurinRate_NothingIsCharged()
    {
        Member(GroupA, new DateTime(2025, 1, 1));

        await Accrual().AccrueKurinAsync(Kurin);

        _created.Should().BeEmpty();
    }

    [Fact]
    public async Task Youth_IsChargedFromTheFirstRateUpToTheCurrentQuarter()
    {
        RateFrom(new DuesQuarter(2025, 4));
        var youth = Member(GroupA, new DateTime(2024, 3, 1));

        await Accrual().AccrueKurinAsync(Kurin);

        QuartersOf(youth).Should().Equal(new DuesQuarter(2025, 4), new DuesQuarter(2026, 1), new DuesQuarter(2026, 2));
        _created.Should().OnlyContain(c => c.GroupKey == GroupA && c.KurinKey == Kurin);
    }

    [Fact]
    public async Task Youth_IsChargedFromTheQuarterTheyJoined()
    {
        RateFrom(new DuesQuarter(2025, 1));
        var youth = Member(GroupA, new DateTime(2026, 2, 20));

        await Accrual().AccrueKurinAsync(Kurin);

        QuartersOf(youth).Should().Equal(new DuesQuarter(2026, 1), new DuesQuarter(2026, 2));
    }

    [Fact]
    public async Task TheQuarter_TurnsAtMidnightInTheKrai_NotInUtc()
    {
        // 22:30 UTC on 30 June is already 1 July in Kyiv, and still 30 June in Toronto.
        _time = new FixedTimeProvider(new DateTimeOffset(2026, 6, 30, 22, 30, 0, TimeSpan.Zero));
        RateFrom(new DuesQuarter(2026, 1));
        var youth = Member(GroupA, new DateTime(2026, 6, 30, 22, 0, 0, DateTimeKind.Utc));

        await Accrual().AccrueKurinAsync(Kurin);

        QuartersOf(youth).Should().Equal(new DuesQuarter(2026, 3));
    }

    [Fact]
    public async Task AKraiBehindUtc_IsNotChargedTheNextQuarterEarly()
    {
        _time = new FixedTimeProvider(new DateTimeOffset(2026, 6, 30, 22, 30, 0, TimeSpan.Zero));
        _zone = "America/Toronto";
        RateFrom(new DuesQuarter(2026, 1));
        var youth = Member(GroupA, new DateTime(2026, 5, 1));

        await Accrual().AccrueKurinAsync(Kurin);

        QuartersOf(youth).Should().Equal(new DuesQuarter(2026, 2));
    }

    [Fact]
    public async Task StaffAndPeopleOutsideAGroup_AreNotCharged()
    {
        RateFrom(new DuesQuarter(2026, 1));
        Member(null, new DateTime(2025, 1, 1));
        Member(GroupA, new DateTime(2025, 1, 1), MembershipKind.Staff);

        await Accrual().AccrueKurinAsync(Kurin);

        _created.Should().BeEmpty();
    }

    [Fact]
    public async Task Leaving_IsNotChargedTheQuarterTheyLeftIn()
    {
        RateFrom(new DuesQuarter(2025, 3));
        var youth = Member(GroupA, new DateTime(2025, 1, 1), left: new DateTime(2025, 11, 2));

        await Accrual().AccrueKurinAsync(Kurin);

        QuartersOf(youth).Should().Equal(new DuesQuarter(2025, 3));
    }

    [Fact]
    public async Task PayingInArrears_TheRunningQuarterIsNotChargedYet()
    {
        _settings = new KurinDuesSettings { KurinKey = Kurin, ChargeTiming = DuesChargeTiming.InArrears };
        RateFrom(new DuesQuarter(2025, 4));
        var youth = Member(GroupA, new DateTime(2024, 3, 1));

        await Accrual().AccrueKurinAsync(Kurin);

        QuartersOf(youth).Should().Equal(new DuesQuarter(2025, 4), new DuesQuarter(2026, 1));
    }

    [Fact]
    public async Task PayingInArrears_LeavingAtTheStartOfAQuarter_OwesTheOneBefore()
    {
        _settings = new KurinDuesSettings { KurinKey = Kurin, ChargeTiming = DuesChargeTiming.InArrears };
        RateFrom(new DuesQuarter(2025, 4));
        var youth = Member(GroupA, new DateTime(2024, 3, 1), left: new DateTime(2026, 4, 3));

        await Accrual().AccrueKurinAsync(Kurin);

        QuartersOf(youth).Should().Equal(new DuesQuarter(2025, 4), new DuesQuarter(2026, 1));
    }

    [Fact]
    public async Task Accruing_TwiceChargesNothingTwice()
    {
        RateFrom(new DuesQuarter(2026, 1));
        Member(GroupA, new DateTime(2025, 1, 1));

        await Accrual().AccrueKurinAsync(Kurin);
        await Accrual().AccrueKurinAsync(Kurin);

        _created.Should().HaveCount(2);
        _dues.Verify(d => d.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task MovingAPerson_ChargesTheQuartersSoFarToTheGroupTheyLeave()
    {
        RateFrom(new DuesQuarter(2026, 1));
        var youth = Member(GroupB, new DateTime(2025, 1, 1));
        var handler = new MembershipMovedToGroupEventHandler(Accrual());

        await handler.Handle(
            new DomainEventNotification<MembershipMovedToGroup>(new MembershipMovedToGroup(youth.MembershipKey, Kurin, GroupA, GroupB)),
            CancellationToken.None);
        await Accrual().AccrueKurinAsync(Kurin);

        _created.Should().HaveCount(2).And.OnlyContain(c => c.GroupKey == GroupA);
    }

    private void TagRate(Guid tag, DuesQuarter from, decimal kurinShare) =>
        _rates.Add(new KurinDuesRate { KurinKey = Kurin, TagKey = tag, FromQuarter = from.Index, KurinShare = kurinShare });

    private DuesCharge Charged(KurinMembershipRecord membership, DuesQuarter quarter, Guid? tag = null)
    {
        var charge = new DuesCharge { KurinKey = Kurin, MembershipKey = membership.MembershipKey, Quarter = quarter.Index, RateTagKey = tag };
        _existing.Add(charge);
        return charge;
    }

    private void Paid(KurinMembershipRecord membership, decimal amount) => _entries.Add(new DuesEntry
    {
        KurinKey = Kurin,
        MembershipKey = membership.MembershipKey,
        Kind = DuesEntryKind.Contribution,
        Amount = amount
    });

    [Fact]
    public async Task ARateSetBackwards_RepricesTheQuartersNotPaidInFull_FromItsQuarter()
    {
        var usp = Guid.NewGuid();
        RateFrom(new DuesQuarter(2025, 4));
        var person = Member(null, new DateTime(2025, 1, 1));
        Senior((person.MemberKey, [usp]));
        var q4 = Charged(person, new DuesQuarter(2025, 4));
        var q1 = Charged(person, new DuesQuarter(2026, 1));
        var q2 = Charged(person, new DuesQuarter(2026, 2));
        // The rate for everyone is 255 a quarter: the last quarter of 2025 and the first of 2026 are paid.
        Paid(person, 510);
        TagRate(usp, new DuesQuarter(2025, 4), 300);

        await Accrual().RepriceKurinAsync(Kurin, new DuesQuarter(2025, 4));

        q4.RateTagKey.Should().BeNull();
        q1.RateTagKey.Should().BeNull();
        q2.RateTagKey.Should().Be(usp);
        _dues.Verify(d => d.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ARateSetBackwards_LeavesTheDebtOfSomeoneWhoLeft()
    {
        // Their tags no longer count in the kurin; that must not turn their debt into the rate for everyone.
        var usp = Guid.NewGuid();
        RateFrom(new DuesQuarter(2026, 1));
        TagRate(usp, new DuesQuarter(2026, 1), 300);
        var gone = Member(null, new DateTime(2025, 1, 1), left: new DateTime(2026, 4, 2));
        var q1 = Charged(gone, new DuesQuarter(2026, 1), usp);
        Senior();

        await Accrual().RepriceKurinAsync(Kurin, new DuesQuarter(2026, 1));

        q1.RateTagKey.Should().Be(usp);
    }

    [Fact]
    public async Task ADeletedTag_LeavesNoUnpaidQuarterOnItsRate()
    {
        var usp = Guid.NewGuid();
        RateFrom(new DuesQuarter(2025, 4));
        TagRate(usp, new DuesQuarter(2025, 4), 300);
        var person = Member(null, new DateTime(2025, 1, 1));
        var q4 = Charged(person, new DuesQuarter(2025, 4), usp);
        var q1 = Charged(person, new DuesQuarter(2026, 1), usp);
        // The tag's rate is 345 a quarter: the first quarter is paid and keeps the price it was paid at.
        Paid(person, 345);
        Senior();

        await Accrual().RepriceKurinAsync(Kurin, DuesQuarter.FromIndex(0));

        q4.RateTagKey.Should().Be(usp);
        q1.RateTagKey.Should().BeNull();
    }

    [Fact]
    public async Task NothingToReprice_ReadsNoLedger()
    {
        var usp = Guid.NewGuid();
        RateFrom(new DuesQuarter(2026, 1));
        TagRate(usp, new DuesQuarter(2026, 1), 300);
        var person = Member(null, new DateTime(2025, 1, 1));
        Charged(person, new DuesQuarter(2026, 1), usp);
        Charged(person, new DuesQuarter(2026, 2), usp);
        Senior((person.MemberKey, [usp]));

        await Accrual().AccrueKurinAsync(Kurin);

        _dues.VerifyGet(d => d.DuesEntries, Times.Never);
    }

    [Fact]
    public async Task ARateSetBackwards_LeavesTheQuartersBeforeIt()
    {
        var usp = Guid.NewGuid();
        RateFrom(new DuesQuarter(2026, 1));
        var person = Member(null, new DateTime(2025, 1, 1));
        Senior((person.MemberKey, [usp]));
        var q1 = Charged(person, new DuesQuarter(2026, 1));
        var q2 = Charged(person, new DuesQuarter(2026, 2));
        TagRate(usp, new DuesQuarter(2026, 1), 300);

        await Accrual().RepriceKurinAsync(Kurin, new DuesQuarter(2026, 2));

        q1.RateTagKey.Should().BeNull();
        q2.RateTagKey.Should().Be(usp);
    }

    [Fact]
    public async Task ATagPutOnAfterTheQuarterWasCharged_MovesTheRunningQuarterOnly()
    {
        var usp = Guid.NewGuid();
        RateFrom(new DuesQuarter(2026, 1));
        TagRate(usp, new DuesQuarter(2026, 1), 300);
        var person = Member(null, new DateTime(2025, 1, 1));
        var q1 = Charged(person, new DuesQuarter(2026, 1));
        var q2 = Charged(person, new DuesQuarter(2026, 2));
        Senior((person.MemberKey, [usp]));

        await Accrual().AccrueKurinAsync(Kurin);

        q1.RateTagKey.Should().BeNull();
        q2.RateTagKey.Should().Be(usp);
    }

    [Fact]
    public async Task ATagTakenOff_MovesTheRunningQuarterBackToTheRateForEveryone()
    {
        var usp = Guid.NewGuid();
        RateFrom(new DuesQuarter(2026, 1));
        TagRate(usp, new DuesQuarter(2026, 1), 300);
        var person = Member(null, new DateTime(2025, 1, 1));
        Charged(person, new DuesQuarter(2026, 1), usp);
        var q2 = Charged(person, new DuesQuarter(2026, 2), usp);
        // With the kurin's tags off nobody carries one, as when the tag is taken off.
        Senior();

        await Accrual().AccrueKurinAsync(Kurin);

        q2.RateTagKey.Should().BeNull();
    }

    [Fact]
    public async Task AQuarterPaidInFull_KeepsTheTagItWasPaidAt()
    {
        var usp = Guid.NewGuid();
        RateFrom(new DuesQuarter(2026, 2));
        TagRate(usp, new DuesQuarter(2026, 2), 300);
        var person = Member(null, new DateTime(2025, 1, 1));
        var q2 = Charged(person, new DuesQuarter(2026, 2), usp);
        Paid(person, 300);
        Senior();

        await Accrual().AccrueKurinAsync(Kurin);

        q2.RateTagKey.Should().Be(usp);
        _dues.Verify(d => d.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task AnUpyuKurin_IsNeverRepriced()
    {
        RateFrom(new DuesQuarter(2026, 1));
        var youth = Member(GroupA, new DateTime(2025, 1, 1));
        Charged(youth, new DuesQuarter(2026, 1));

        await Accrual().RepriceKurinAsync(Kurin, new DuesQuarter(2026, 1));

        _dues.Verify(d => d.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    private MembershipRecord LeftElsewhere(DateTime joined, DateTime left)
    {
        var record = new MembershipRecord(Guid.NewGuid(), Guid.NewGuid(), 1, KurinBranch.UPYu, null, GroupB, "Г", MembershipKind.Youth, joined, left);
        _history.Add(record);
        return record;
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task MovingFromAnotherKurin_StartsAfterTheQuarterTheOldKurinKept(bool senior)
    {
        RateFrom(new DuesQuarter(2025, 4));
        var arrived = Member(senior ? null : GroupA, new DateTime(2026, 2, 10));
        var before = LeftElsewhere(new DateTime(2024, 9, 1), new DateTime(2026, 2, 9));
        _elsewhere.Add(new DuesCharge { KurinKey = Guid.NewGuid(), MembershipKey = before.MembershipKey, Quarter = new DuesQuarter(2026, 1).Index });
        if (senior)
        {
            Senior();
        }

        await Accrual().AccrueKurinAsync(Kurin);

        QuartersOf(arrived).Should().Equal(new DuesQuarter(2026, 2));
    }

    [Fact]
    public async Task MovingFromAnotherKurin_ThatDroppedTheUnpaidQuarter_IsChargedItHere()
    {
        RateFrom(new DuesQuarter(2025, 4));
        var arrived = Member(GroupA, new DateTime(2026, 2, 10));
        LeftElsewhere(new DateTime(2024, 9, 1), new DateTime(2026, 2, 9));

        await Accrual().AccrueKurinAsync(Kurin);

        QuartersOf(arrived).Should().Equal(new DuesQuarter(2026, 1), new DuesQuarter(2026, 2));
    }

    [Fact]
    public async Task MovingFromAnotherKurin_InAnEarlierQuarter_ChargesTheQuarterOfArrival()
    {
        RateFrom(new DuesQuarter(2025, 4));
        var arrived = Member(GroupA, new DateTime(2026, 2, 10));
        var before = LeftElsewhere(new DateTime(2024, 9, 1), new DateTime(2025, 12, 20));
        _elsewhere.Add(new DuesCharge { KurinKey = Guid.NewGuid(), MembershipKey = before.MembershipKey, Quarter = new DuesQuarter(2025, 4).Index });

        await Accrual().AccrueKurinAsync(Kurin);

        QuartersOf(arrived).Should().Equal(new DuesQuarter(2026, 1), new DuesQuarter(2026, 2));
    }
}
