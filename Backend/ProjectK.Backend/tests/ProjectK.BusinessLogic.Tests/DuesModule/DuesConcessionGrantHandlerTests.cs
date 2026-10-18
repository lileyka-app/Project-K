using FluentAssertions;
using Moq;
using ProjectK.BusinessLogic.Modules.DuesModule.Features.Group.Grants;
using ProjectK.BusinessLogic.Modules.DuesModule.Services;
using ProjectK.Common.Entities.DuesModule;
using ProjectK.Common.Entities.KurinModule;
using ProjectK.Common.Interfaces;
using ProjectK.Common.Interfaces.Modules.DuesModule;
using ProjectK.Common.Interfaces.Modules.InfrastructureModule;
using ProjectK.Common.Interfaces.Modules.KraiModule;
using ProjectK.Common.Interfaces.Modules.KurinModule;
using ProjectK.Common.Models.Dtos.DuesModule.Requests;
using ProjectK.Common.Models.Dues;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.KraiProfiles;
using ProjectK.Common.Models.Records;
using Xunit;

namespace ProjectK.BusinessLogic.Tests.DuesModule;

public class DuesConcessionGrantHandlerTests
{
    private static readonly Guid Actor = Guid.NewGuid();
    private static readonly DuesConcessionRule Sibling = new("sibling", "Брат чи сестра", KraiDues.Percent, 20, DuesParts.Stanytsia);

    private readonly Kurin _kurin = new(7);
    private readonly Group _group;
    private readonly KurinMembershipRecord _youth;
    private readonly KurinMembershipRecord _elsewhere;
    private readonly List<DuesConcessionGrant> _grants = [];
    private readonly Mock<IDuesUnitOfWork> _dues = new();
    private readonly Mock<ICurrentUserContext> _user = new();

    public DuesConcessionGrantHandlerTests()
    {
        _group = new Group("Alpha", _kurin.KurinKey);
        _youth = new KurinMembershipRecord(Guid.NewGuid(), Guid.NewGuid(), _group.GroupKey, MembershipKind.Youth, DateTime.UtcNow, null);
        _elsewhere = new KurinMembershipRecord(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), MembershipKind.Youth, DateTime.UtcNow, null);

        var grants = new Mock<IDuesConcessionGrantRepository>();
        grants.Setup(r => r.Create(It.IsAny<DuesConcessionGrant>(), It.IsAny<CancellationToken>()))
            .Callback<DuesConcessionGrant, CancellationToken>((g, _) => _grants.Add(g));
        grants.Setup(r => r.GetForKurinAsync(_kurin.KurinKey, It.IsAny<CancellationToken>())).ReturnsAsync(() => _grants);
        grants.Setup(r => r.GetForUpdateAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid key, CancellationToken _) => _grants.FirstOrDefault(g => g.DuesConcessionGrantKey == key));
        grants.Setup(r => r.Delete(It.IsAny<DuesConcessionGrant>(), It.IsAny<CancellationToken>()))
            .Callback<DuesConcessionGrant, CancellationToken>((g, _) => _grants.Remove(g));
        _dues.SetupGet(d => d.DuesConcessionGrants).Returns(grants.Object);
        _user.SetupGet(u => u.UserId).Returns(Actor);
        _user.SetupGet(u => u.KurinKey).Returns(_kurin.KurinKey);
    }

    private GroupDuesAccess Access()
    {
        var groups = new Mock<IGroupRepository>();
        groups.Setup(g => g.GetByKeyAsync(_group.GroupKey, It.IsAny<CancellationToken>())).ReturnsAsync(_group);
        var uow = new Mock<IUnitOfWork>();
        uow.SetupGet(u => u.Groups).Returns(groups.Object);
        return new GroupDuesAccess(uow.Object, _user.Object);
    }

    private IMembershipDirectory Memberships()
    {
        var memberships = new Mock<IMembershipDirectory>();
        memberships.Setup(m => m.GetInKurinAsync(_kurin.KurinKey, It.IsAny<CancellationToken>())).ReturnsAsync([_youth, _elsewhere]);
        return memberships.Object;
    }

    private IKraiProfileProvider Profiles()
    {
        var profile = new KraiProfile(
            "cz", "Чехія", 2,
            new KraiCatalogSettings(["ua"], []),
            new KraiLocaleSettings("CZK", "Europe/Prague", "uk-UA"),
            KraiCardFieldsSettings.Default,
            new KraiDuesSettings(KraiDues.Stanytsia) { Concessions = [Sibling] });
        var profiles = new Mock<IKraiProfileProvider>();
        profiles.Setup(p => p.GetForKurinAsync(_kurin.KurinKey, It.IsAny<CancellationToken>())).ReturnsAsync(profile);
        return profiles.Object;
    }

    private Task<ServiceResult<Guid>> GrantAsync(Guid membershipKey, string key, (int Year, int Number) from, (int Year, int Number)? to = null, string? reason = null) =>
        new GrantDuesConcessionCommandHandler(Access(), _dues.Object, Memberships(), Profiles(), _user.Object)
            .Handle(new GrantDuesConcessionCommand(_group.GroupKey, membershipKey, new GrantDuesConcessionRequest
            {
                ConcessionKey = key,
                FromQuarter = new QuarterRequest { Year = from.Year, Number = from.Number },
                ToQuarter = to is { } t ? new QuarterRequest { Year = t.Year, Number = t.Number } : null,
                Reason = reason
            }), CancellationToken.None);

    [Fact]
    public async Task Granting_CopiesTheKraisRuleOntoTheGrant()
    {
        var result = await GrantAsync(_youth.MembershipKey, "sibling", (2026, 2), (2026, 4), "  молодший брат у курені  ");

        result.Type.Should().Be(ResultType.Created);
        var grant = _grants.Single();
        grant.Name.Should().Be("Брат чи сестра");
        grant.Kind.Should().Be(KraiDues.Percent);
        grant.Value.Should().Be(20);
        grant.Parts.Should().Be(DuesParts.Stanytsia);
        grant.FromQuarter.Should().Be(new DuesQuarter(2026, 2).Index);
        grant.ToQuarter.Should().Be(new DuesQuarter(2026, 4).Index);
        grant.Reason.Should().Be("молодший брат у курені");
        grant.SetByUserKey.Should().Be(Actor);
    }

    [Fact]
    public async Task APilhaTheKraiDoesNotHave_IsRefused()
    {
        (await GrantAsync(_youth.MembershipKey, "made-up", (2026, 2))).Type.Should().Be(ResultType.BadRequest);
        _grants.Should().BeEmpty();
    }

    [Fact]
    public async Task SomeoneOfAnotherGroup_IsNotFound()
    {
        (await GrantAsync(_elsewhere.MembershipKey, "sibling", (2026, 2))).Type.Should().Be(ResultType.NotFound);
    }

    [Fact]
    public async Task AnEndBeforeTheStart_IsRefused()
    {
        (await GrantAsync(_youth.MembershipKey, "sibling", (2026, 3), (2026, 2))).Type.Should().Be(ResultType.BadRequest);
    }

    [Fact]
    public async Task TheSamePilhaOverTheSameQuarters_IsAConflict()
    {
        await GrantAsync(_youth.MembershipKey, "sibling", (2026, 1));

        (await GrantAsync(_youth.MembershipKey, "sibling", (2026, 3), (2026, 4))).Type.Should().Be(ResultType.Conflict);
    }

    [Fact]
    public async Task Ending_SetsTheLastQuarter_AndDeleting_RemovesIt()
    {
        await GrantAsync(_youth.MembershipKey, "sibling", (2026, 1));
        var grant = _grants.Single();

        var ended = await new UpdateDuesConcessionGrantCommandHandler(Access(), _dues.Object, Memberships(), _user.Object)
            .Handle(new UpdateDuesConcessionGrantCommand(_group.GroupKey, grant.DuesConcessionGrantKey, new UpdateDuesConcessionGrantRequest
            {
                ToQuarter = new QuarterRequest { Year = 2026, Number = 2 },
                Reason = "переїхали"
            }), CancellationToken.None);

        ended.Type.Should().Be(ResultType.Success);
        grant.ToQuarter.Should().Be(new DuesQuarter(2026, 2).Index);
        grant.Reason.Should().Be("переїхали");

        var deleted = await new DeleteDuesConcessionGrantCommandHandler(Access(), _dues.Object, Memberships())
            .Handle(new DeleteDuesConcessionGrantCommand(_group.GroupKey, grant.DuesConcessionGrantKey), CancellationToken.None);

        deleted.Type.Should().Be(ResultType.Success);
        _grants.Should().BeEmpty();
    }
}
