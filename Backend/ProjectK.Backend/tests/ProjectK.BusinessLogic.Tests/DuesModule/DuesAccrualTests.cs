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
    private readonly Mock<IDuesUnitOfWork> _dues = new();

    public DuesAccrualTests()
    {
        var rates = new Mock<IKurinDuesRateRepository>();
        rates.Setup(r => r.GetForKurinAsync(Kurin, It.IsAny<CancellationToken>())).ReturnsAsync(() => _rates);
        var charges = new Mock<IDuesChargeRepository>();
        charges.Setup(r => r.GetForKurinAsync(Kurin, It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => _existing.Concat(_created).ToList());
        charges.Setup(r => r.Create(It.IsAny<DuesCharge>(), It.IsAny<CancellationToken>()))
            .Callback<DuesCharge, CancellationToken>((charge, _) => _created.Add(charge));
        _dues.SetupGet(d => d.KurinDuesRates).Returns(rates.Object);
        _dues.SetupGet(d => d.DuesCharges).Returns(charges.Object);
    }

    private DuesAccrual Accrual()
    {
        var directory = new Mock<IMembershipDirectory>();
        directory.Setup(d => d.GetInKurinAsync(Kurin, It.IsAny<CancellationToken>())).ReturnsAsync(() => _memberships);
        var profiles = new Mock<IKraiProfileProvider>();
        if (_zone is not null)
        {
            profiles.Setup(p => p.GetForKurinAsync(Kurin, It.IsAny<CancellationToken>())).ReturnsAsync(new KraiProfile(
                "ca", "Канада", 1, new KraiCatalogSettings(["ca"], []), new KraiLocaleSettings("CAD", _zone, "en-CA"), KraiCardFieldsSettings.Default, KraiDuesSettings.Default));
        }

        return new DuesAccrual(_dues.Object, directory.Object, _time, profiles.Object);
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
    public async Task Leaving_StopsChargesAfterTheQuarterTheyLeft()
    {
        RateFrom(new DuesQuarter(2025, 3));
        var youth = Member(GroupA, new DateTime(2025, 1, 1), left: new DateTime(2025, 11, 2));

        await Accrual().AccrueKurinAsync(Kurin);

        QuartersOf(youth).Should().Equal(new DuesQuarter(2025, 3), new DuesQuarter(2025, 4));
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
}
