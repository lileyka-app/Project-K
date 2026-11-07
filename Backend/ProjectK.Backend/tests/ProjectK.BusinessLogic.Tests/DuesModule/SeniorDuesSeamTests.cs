using FluentAssertions;
using Moq;
using ProjectK.BusinessLogic.Modules.DuesModule.Services;
using ProjectK.BusinessLogic.Modules.MeModule.Features.Dues;
using ProjectK.BusinessLogic.Modules.MeModule.Services;
using ProjectK.BusinessLogic.Tests.TestHelpers;
using ProjectK.Common.Entities.DuesModule;
using ProjectK.Common.Entities.KurinModule;
using ProjectK.Common.Entities.KurinModule.Tags;
using ProjectK.Common.Interfaces;
using ProjectK.Common.Interfaces.Modules.DuesModule;
using ProjectK.Common.Interfaces.Modules.InfrastructureModule;
using ProjectK.Common.Interfaces.Modules.KraiModule;
using ProjectK.Common.Interfaces.Modules.KurinModule;
using ProjectK.Common.Interfaces.Modules.MemberModule;
using ProjectK.Common.Models.Dues;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Records;
using Xunit;

namespace ProjectK.BusinessLogic.Tests.DuesModule;

/// <summary>A senior kurin's tags, as the dues module reads them, and the rate a senior sees as theirs.</summary>
public class SeniorDuesSeamTests
{
    private static readonly Guid KurinKey = Guid.NewGuid();
    private static readonly Guid Tag = Guid.NewGuid();
    private static readonly Guid MemberKey = Guid.NewGuid();
    private static readonly DuesQuarter Now = new(2026, 2);

    private static IUnitOfWork Kurin(bool? tagsOn)
    {
        var kurins = new Mock<IKurinRepository>();
        kurins.Setup(k => k.GetByKeyAsync(KurinKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Kurin(1, KurinBranch.USP) { KurinKey = KurinKey });
        var tags = new Mock<IKurinTagRepository>();
        tags.Setup(t => t.GetSettingsAsync(KurinKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(tagsOn is { } on ? new KurinTagSettings { KurinKey = KurinKey, Enabled = on } : null);
        tags.Setup(t => t.GetAssignmentsAsync(KurinKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync([new MemberTag { MemberKey = MemberKey, KurinTagKey = Tag }]);
        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork.SetupGet(u => u.Kurins).Returns(kurins.Object);
        unitOfWork.SetupGet(u => u.KurinTags).Returns(tags.Object);
        return unitOfWork.Object;
    }

    [Theory]
    [InlineData(null, true)]
    [InlineData(true, true)]
    [InlineData(false, false)]
    public async Task WithTheKurinsTagsOff_NobodyCarriesATagForTheRates(bool? stored, bool counted)
    {
        var shape = new DuesKurinShape(Kurin(stored));

        var tags = await shape.TagsByMemberAsync(KurinKey);

        (await shape.TagsOnAsync(KurinKey)).Should().Be(counted);
        tags.ContainsKey(MemberKey).Should().Be(counted);
    }

    private static readonly KurinMembershipRecord Membership =
        new(Guid.NewGuid(), MemberKey, null, MembershipKind.Youth, new DateTime(2025, 1, 1), null);

    private static DuesLedger Ledger() => new(
        [
            new KurinDuesRate { KurinKey = KurinKey, FromQuarter = Now.Index, KurinShare = 400 },
            new KurinDuesRate { KurinKey = KurinKey, TagKey = Tag, FromQuarter = Now.Index, KraiFull = 300, KurinShare = 150 }
        ],
        [],
        [],
        [new DuesCharge { KurinKey = KurinKey, MembershipKey = Membership.MembershipKey, Quarter = Now.Index, RateTagKey = Tag }],
        []);

    private static async Task<DuesNames> Names()
    {
        var memberships = new Mock<IMembershipDirectory>();
        memberships.Setup(m => m.GetInKurinAsync(KurinKey, It.IsAny<CancellationToken>())).ReturnsAsync([Membership]);
        var members = new Mock<IMemberDirectory>();
        members.Setup(m => m.GetByKurinAsync(KurinKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync([new MemberSummary(MemberKey, Guid.NewGuid(), Guid.Empty, null, "Ірина", "Коваль", "i@example.org", null)]);
        return await DuesNames.LoadAsync(memberships.Object, members.Object, KurinKey, CancellationToken.None);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task TheRegister_NamesTheTagAQuarterIsPricedBy_OnlyWhereTheViewerSeesThePersonsTags(bool sees)
    {
        var ledger = Ledger();
        var lens = new DuesTagLens([], new Dictionary<Guid, IReadOnlyCollection<Guid>> { [MemberKey] = [Tag] }, (_, _) => sees);

        var row = DuesAccountRows.Map(ledger.Accounts(), null, ledger, await Names(), [], Now, lens).Single();

        row.RateTagKey.Should().Be(sees ? Tag : null);
        row.TagKeys.Should().HaveCount(sees ? 1 : 0);
        row.RateNow.Should().Be(450);
    }

    [Fact]
    public async Task MyDues_ShowASeniorTheQuartersRate_OfTheirTag()
    {
        var userKey = Guid.NewGuid();
        var user = new Mock<ICurrentUserContext>();
        user.SetupGet(u => u.UserId).Returns(userKey);
        var memberships = new Mock<IMembershipDirectory>();
        memberships.Setup(m => m.GetCurrentForAccountAsync(userKey, It.IsAny<CancellationToken>())).ReturnsAsync(
            [new MembershipRecord(Membership.MembershipKey, KurinKey, 1, KurinBranch.USP, null, null, null, MembershipKind.Youth, Membership.JoinedAtUtc, null)]);
        var members = new Mock<IMemberDirectory>();
        members.Setup(m => m.FindByAccountAsync(userKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MemberSummary(MemberKey, userKey, Guid.Empty, null, "Ірина", "Коваль", "i@example.org", null));
        members.Setup(m => m.GetLevelsAsync(MemberKey, It.IsAny<CancellationToken>())).ReturnsAsync([]);

        var dues = new Mock<IDuesUnitOfWork>();
        var rates = new Mock<IKurinDuesRateRepository>();
        rates.Setup(r => r.GetForKurinAsync(KurinKey, It.IsAny<CancellationToken>())).ReturnsAsync(
        [
            new KurinDuesRate { KurinKey = KurinKey, FromQuarter = Now.Index, KurinShare = 400 },
            new KurinDuesRate { KurinKey = KurinKey, TagKey = Tag, FromQuarter = Now.Index, KraiFull = 300, KurinShare = 150 }
        ]);
        var charges = new Mock<IDuesChargeRepository>();
        charges.Setup(c => c.GetForKurinAsync(KurinKey, It.IsAny<CancellationToken>())).ReturnsAsync(
            [new DuesCharge { KurinKey = KurinKey, MembershipKey = Membership.MembershipKey, Quarter = Now.Index, RateTagKey = Tag }]);
        dues.SetupGet(d => d.KurinDuesRates).Returns(rates.Object);
        dues.SetupGet(d => d.DuesCharges).Returns(charges.Object);
        dues.SetupGet(d => d.GroupDuesRates).Returns(Mock.Of<IGroupDuesRateRepository>(r => r.GetForKurinAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()) == Task.FromResult<IReadOnlyList<GroupDuesRate>>(Array.Empty<GroupDuesRate>())));
        dues.SetupGet(d => d.DuesConcessions).Returns(Mock.Of<IDuesConcessionRepository>(r => r.GetForKurinAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()) == Task.FromResult<IReadOnlyList<DuesConcession>>(Array.Empty<DuesConcession>())));
        dues.SetupGet(d => d.DuesConcessionGrants).Returns(Mock.Of<IDuesConcessionGrantRepository>(r => r.GetForKurinAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()) == Task.FromResult<IReadOnlyList<DuesConcessionGrant>>(Array.Empty<DuesConcessionGrant>())));
        dues.SetupGet(d => d.DuesEntries).Returns(Mock.Of<IDuesEntryRepository>(r => r.GetForKurinAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()) == Task.FromResult<IReadOnlyList<DuesEntry>>(Array.Empty<DuesEntry>())));

        var handler = new GetMyDuesQueryHandler(
            new MePerson(user.Object, memberships.Object, members.Object),
            new DuesLedgerReader(dues.Object, Mock.Of<IDuesAccrual>()),
            user.Object,
            new FixedTimeProvider(new DateTimeOffset(2026, 5, 10, 12, 0, 0, TimeSpan.Zero)),
            Mock.Of<IKraiProfileProvider>());

        var result = await handler.Handle(new GetMyDuesQuery(), CancellationToken.None);

        result.Data!.Single().QuarterRate.Should().Be(450);
    }
}
