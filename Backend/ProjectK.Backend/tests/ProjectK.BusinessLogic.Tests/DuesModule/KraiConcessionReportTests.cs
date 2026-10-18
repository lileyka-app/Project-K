using FluentAssertions;
using Moq;
using ProjectK.BusinessLogic.Modules.DuesModule.Features.Krai;
using ProjectK.BusinessLogic.Modules.DuesModule.Services;
using ProjectK.BusinessLogic.Tests.TestHelpers;
using ProjectK.Common.Entities.DuesModule;
using ProjectK.Common.Entities.KurinModule;
using ProjectK.Common.Interfaces;
using ProjectK.Common.Interfaces.Modules.DuesModule;
using ProjectK.Common.Interfaces.Modules.KraiModule;
using ProjectK.Common.Interfaces.Modules.KurinModule;
using ProjectK.Common.Models.Dues;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.KraiProfiles;
using Xunit;

namespace ProjectK.BusinessLogic.Tests.DuesModule;

public class KraiConcessionReportTests
{
    private static readonly Kurin Kurin = new(12) { KraiCode = "cz" };
    private static readonly Guid Group = Guid.NewGuid();
    private static readonly Guid Youth = Guid.NewGuid();
    private static readonly Guid Sister = Guid.NewGuid();
    private static readonly DuesQuarter Q4 = new(2026, 4);

    private readonly List<DuesConcessionGrant> _grants = [];
    private readonly List<DuesConcession> _concessions = [];

    private GetKraiConcessionReportQueryHandler Handler(DateTimeOffset now)
    {
        IReadOnlyList<KurinDuesRate> kurinRates = [new KurinDuesRate { KurinKey = Kurin.KurinKey, FromQuarter = Q4.Index, StanytsiaFull = 240, StanytsiaReduced = 180, KurinShare = 60 }];
        IReadOnlyList<GroupDuesRate> groupRates = [];
        IReadOnlyList<DuesCharge> charges =
        [
            new DuesCharge { KurinKey = Kurin.KurinKey, GroupKey = Group, MembershipKey = Youth, Quarter = Q4.Index },
            new DuesCharge { KurinKey = Kurin.KurinKey, GroupKey = Group, MembershipKey = Sister, Quarter = Q4.Index }
        ];
        IReadOnlyList<DuesEntry> entries = [];
        IReadOnlyList<DuesConcession> concessions = _concessions;
        IReadOnlyList<DuesConcessionGrant> grants = _grants;

        var dues = new Mock<IDuesUnitOfWork>();
        dues.SetupGet(d => d.KurinDuesRates).Returns(Mock.Of<IKurinDuesRateRepository>(r => r.GetForKurinAsync(Kurin.KurinKey, It.IsAny<CancellationToken>()) == Task.FromResult(kurinRates)));
        dues.SetupGet(d => d.GroupDuesRates).Returns(Mock.Of<IGroupDuesRateRepository>(r => r.GetForKurinAsync(Kurin.KurinKey, It.IsAny<CancellationToken>()) == Task.FromResult(groupRates)));
        dues.SetupGet(d => d.DuesConcessions).Returns(Mock.Of<IDuesConcessionRepository>(r => r.GetForKurinAsync(Kurin.KurinKey, It.IsAny<CancellationToken>()) == Task.FromResult(concessions)));
        dues.SetupGet(d => d.DuesCharges).Returns(Mock.Of<IDuesChargeRepository>(r => r.GetForKurinAsync(Kurin.KurinKey, It.IsAny<CancellationToken>()) == Task.FromResult(charges)));
        dues.SetupGet(d => d.DuesEntries).Returns(Mock.Of<IDuesEntryRepository>(r => r.GetForKurinAsync(Kurin.KurinKey, It.IsAny<CancellationToken>()) == Task.FromResult(entries)));
        dues.SetupGet(d => d.DuesConcessionGrants).Returns(Mock.Of<IDuesConcessionGrantRepository>(r => r.GetForKurinAsync(Kurin.KurinKey, It.IsAny<CancellationToken>()) == Task.FromResult(grants)));

        IReadOnlyList<Kurin> kurins = [Kurin];
        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork.SetupGet(u => u.Kurins).Returns(Mock.Of<IKurinRepository>(r => r.GetInKraiAsync("cz", It.IsAny<CancellationToken>()) == Task.FromResult(kurins)));

        var profile = new KraiProfile(
            "cz", "Чехія", 3,
            new KraiCatalogSettings(["ua"], []),
            new KraiLocaleSettings("CZK", "Europe/Prague", "uk-UA"),
            KraiCardFieldsSettings.Default,
            new KraiDuesSettings(KraiDues.Stanytsia) { Concessions = [new DuesConcessionRule("sibling", "Брат чи сестра", KraiDues.Percent, 50, DuesParts.Stanytsia)] });
        var profiles = new Mock<IKraiProfileProvider>();
        profiles.Setup(p => p.GetForKraiAsync("cz", It.IsAny<CancellationToken>())).ReturnsAsync(profile);

        return new GetKraiConcessionReportQueryHandler(unitOfWork.Object, new DuesLedgerReader(dues.Object, Mock.Of<IDuesAccrual>()), profiles.Object, new FixedTimeProvider(now));
    }

    private void Grant(Guid membership, string name, string kind, decimal value, DuesParts parts) => _grants.Add(new DuesConcessionGrant
    {
        KurinKey = Kurin.KurinKey,
        MembershipKey = membership,
        ConcessionKey = name == "Звільнення" ? "free" : "sibling",
        Name = name,
        Kind = kind,
        Value = value,
        Parts = parts,
        FromQuarter = Q4.Index
    });

    [Fact]
    public async Task OnlyThePilhaThatSetThePrice_Counts_UnderTheKraisCurrentName()
    {
        Grant(Youth, "Брат (стара назва)", KraiDues.Percent, 50, DuesParts.Stanytsia);
        Grant(Sister, "Брат (стара назва)", KraiDues.Percent, 50, DuesParts.Stanytsia);
        // For the sister the exemption beats 50 % off, so only it counts for her.
        Grant(Sister, "Звільнення", KraiDues.Exempt, 0, DuesParts.Stanytsia | DuesParts.Kurin);

        var result = await Handler(DateTimeOffset.UtcNow).Handle(new GetKraiConcessionReportQuery("cz", 2026, 4), default);

        result.Type.Should().Be(ResultType.Success);
        var rows = result.Data!.Concessions;
        rows.Should().HaveCount(2);
        var sibling = rows.Single(r => r.Key == "sibling");
        sibling.Name.Should().Be("Брат чи сестра");
        sibling.People.Should().Be(1);
        sibling.Reduction.Stanytsia.Should().Be(120);
        var free = rows.Single(r => r.Key == "free");
        free.People.Should().Be(1);
        free.Reduction.Total.Should().Be(300);
        free.Kurins.Single().Number.Should().Be(12);
    }

    [Fact]
    public async Task WithoutAQuarter_ItReadsTheOneRunningByTheKraisClock()
    {
        // 23:30 UTC on 31 December is already 1 January in Prague.
        var result = await Handler(new DateTimeOffset(2026, 12, 31, 23, 30, 0, TimeSpan.Zero)).Handle(new GetKraiConcessionReportQuery("cz"), default);

        result.Data!.Quarter.Year.Should().Be(2027);
        result.Data.Quarter.Number.Should().Be(1);
    }

    [Fact]
    public async Task HalfAQuarter_IsRefused()
    {
        (await Handler(DateTimeOffset.UtcNow).Handle(new GetKraiConcessionReportQuery("cz", 2026, null), default)).Type.Should().Be(ResultType.BadRequest);
    }
}
