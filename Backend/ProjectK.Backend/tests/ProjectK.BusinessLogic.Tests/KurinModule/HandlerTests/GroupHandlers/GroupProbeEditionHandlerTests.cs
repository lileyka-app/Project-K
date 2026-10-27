using FluentAssertions;
using Moq;
using ProjectK.BusinessLogic.Modules.KurinModule.Features.Group.ProbeEdition;
using ProjectK.Common.Entities.KurinModule;
using ProjectK.Common.Interfaces;
using ProjectK.Common.Interfaces.Modules.InfrastructureModule;
using ProjectK.Common.Interfaces.Modules.KurinModule;
using ProjectK.Common.Interfaces.Modules.MemberModule;
using ProjectK.Common.Interfaces.Modules.ProbesAndBadgesModule;
using ProjectK.Common.Models.Audit;
using ProjectK.Common.Models.Catalog;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Records;
using Xunit;

namespace ProjectK.BusinessLogic.Tests.KurinModule.HandlerTests.GroupHandlers;

/// <summary>
/// A гурток chooses among the editions its край is offered, the choice goes to the Журнал дій, and
/// whoever closed a first probe is listed as staying on their own edition.
/// </summary>
public class GroupProbeEditionHandlerTests
{
    private const string New = "upyu-2026";
    private const string NewTitle = "Оновлена проба (2026)";

    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly Mock<IGroupRepository> _groups = new();
    private readonly Mock<IKurinRepository> _kurins = new();
    private readonly Mock<IMembershipRepository> _memberships = new();
    private readonly Mock<IProbesCatalog> _catalog = new();
    private readonly Mock<IKurinAudit> _audit = new();
    private readonly Group _group;
    private readonly Kurin _kurin = new(47) { KraiCode = "ua" };

    public GroupProbeEditionHandlerTests()
    {
        _group = new Group("Соколи", _kurin.KurinKey);
        _unitOfWork.Setup(x => x.Groups).Returns(_groups.Object);
        _unitOfWork.Setup(x => x.Kurins).Returns(_kurins.Object);
        _unitOfWork.Setup(x => x.Memberships).Returns(_memberships.Object);
        _unitOfWork.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
        _groups.Setup(x => x.GetByKeyAsync(_group.GroupKey, It.IsAny<CancellationToken>())).ReturnsAsync(_group);
        _kurins.Setup(x => x.GetByKeyAsync(_kurin.KurinKey, It.IsAny<CancellationToken>())).ReturnsAsync(_kurin);
        _catalog.Setup(x => x.GetAll()).Returns(
        [
            Probe("probe-1", 1, null, "ua"),
            Probe("probe-2", 2, null, "ua"),
            Probe("znak-2026", 0, New, "ua", "trial"),
            Probe("probe-1-2026", 1, New, "ua", "trial"),
            Probe("pl-2027", 1, "pl-2027", "pl"),
        ]);
    }

    private static Probe Probe(string id, int order, string? edition, string krai, string status = "approved") =>
        new(id, id, [new ProbePoint($"{id}-1", "Точка")])
        {
            Order = order,
            Edition = edition,
            EditionTitle = edition == New ? NewTitle : null,
            KraiCode = krai,
            StatusCode = status
        };

    private SetGroupProbeEditionCommandHandler SetHandler() => new(_unitOfWork.Object, _catalog.Object, _audit.Object);

    [Fact]
    public async Task Set_MovesTheGroup_AndWritesTheJournal()
    {
        var result = await SetHandler().Handle(new SetGroupProbeEditionCommand(_group.GroupKey, $" {New} "), CancellationToken.None);

        result.Type.Should().Be(ResultType.Success);
        _group.ProbeEdition.Should().Be(New);
        _audit.Verify(x => x.RecordAsync(
            It.Is<AuditRecord>(r => r.Action == AuditActions.Settings.GroupProbeEditionChanged
                && r.SubjectKey == _group.GroupKey && r.KurinKey == _kurin.KurinKey
                && r.Changes!.Single().From == null && r.Changes!.Single().To == NewTitle),
            It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWork.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Set_RefusesAnEditionOfAnotherKrai()
    {
        var result = await SetHandler().Handle(new SetGroupProbeEditionCommand(_group.GroupKey, "pl-2027"), CancellationToken.None);

        result.Type.Should().Be(ResultType.BadRequest);
        result.ErrorCode.Should().Be("ProbeEditionNotOffered");
        _group.ProbeEdition.Should().BeNull();
    }

    [Fact]
    public async Task Set_TakesAnEmptyChoice_AsTheUnnamedEdition_AndWritesNothingWhenNothingChanges()
    {
        var result = await SetHandler().Handle(new SetGroupProbeEditionCommand(_group.GroupKey, ""), CancellationToken.None);

        result.Type.Should().Be(ResultType.Success);
        _audit.Verify(x => x.RecordAsync(It.IsAny<AuditRecord>(), It.IsAny<CancellationToken>()), Times.Never);
        _unitOfWork.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Get_OffersTheKraisEditions_AndSaysWhereEachYouthStands()
    {
        var held = Guid.NewGuid();
        var free = Guid.NewGuid();
        var staff = Guid.NewGuid();
        _groups.Setup(x => x.GetAllAsync(_kurin.KurinKey, It.IsAny<CancellationToken>())).ReturnsAsync([_group]);
        _memberships.Setup(x => x.GetRecordsInKurinAsync(_kurin.KurinKey, It.IsAny<CancellationToken>())).ReturnsAsync(
        [
            new KurinMembershipRecord(Guid.NewGuid(), held, _group.GroupKey, MembershipKind.Youth, DateTime.UtcNow, null),
            new KurinMembershipRecord(Guid.NewGuid(), free, _group.GroupKey, MembershipKind.Youth, DateTime.UtcNow, null),
            new KurinMembershipRecord(Guid.NewGuid(), staff, _group.GroupKey, MembershipKind.Staff, DateTime.UtcNow, null),
            new KurinMembershipRecord(Guid.NewGuid(), Guid.NewGuid(), _group.GroupKey, MembershipKind.Youth, DateTime.UtcNow, DateTime.UtcNow),
        ]);
        var holds = new Mock<IProbeEditionHolds>();
        holds.Setup(x => x.ForMembersAsync(It.Is<IReadOnlyCollection<Guid>>(keys => keys.Count == 2), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, ProbeEditionHold> { [held] = new(held, null, "probe-1", IsHeld: true) });
        var members = new Mock<IMemberDirectory>();
        members.Setup(x => x.GetByKeysAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>())).ReturnsAsync(
        [
            new MemberSummary(held, null, _kurin.KurinKey, _group.GroupKey, "Олесь", "Коваль", "olesj@example.com", null),
            new MemberSummary(free, null, _kurin.KurinKey, _group.GroupKey, "Ірина", "Бойко", "iryna@example.com", null),
        ]);
        var access = new Mock<IResourceAccessService>();
        access.Setup(x => x.CheckAccessAsync(ResourceType.GroupProbeEdition, ResourceAction.Update, _group.GroupKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ResourceAccessDecision.Allow());

        var result = await new GetKurinProbeEditionsQueryHandler(_unitOfWork.Object, _catalog.Object, holds.Object, members.Object, access.Object)
            .Handle(new GetKurinProbeEditionsQuery(_kurin.KurinKey), CancellationToken.None);

        var dto = result.Data!;
        dto.Editions.Select(e => e.Id).Should().Equal(null, New);
        dto.Editions.Select(e => e.Title).Should().Equal(null, NewTitle);
        dto.Editions[1].IsTrial.Should().BeTrue();
        dto.Editions[1].ProbeTitles.Should().Equal("znak-2026", "probe-1-2026");
        var group = dto.Groups.Should().ContainSingle().Subject;
        group.Edition.Should().BeNull();
        group.CanChange.Should().BeTrue();
        group.Members.Should().Equal(
            new GroupMemberStandingDto(free, "Ірина Бойко", HasStarted: false, Edition: null, ProbeTitle: null, IsHeld: false),
            new GroupMemberStandingDto(held, "Олесь Коваль", HasStarted: true, Edition: null, ProbeTitle: "probe-1", IsHeld: true));
    }

    [Fact]
    public async Task Get_ShowsAnEditionGoneFromTheCatalogue_AsTheUnnamedOne()
    {
        _group.ProbeEdition = "upyu-2030";
        _groups.Setup(x => x.GetAllAsync(_kurin.KurinKey, It.IsAny<CancellationToken>())).ReturnsAsync([_group]);
        _memberships.Setup(x => x.GetRecordsInKurinAsync(_kurin.KurinKey, It.IsAny<CancellationToken>())).ReturnsAsync([]);
        var holds = new Mock<IProbeEditionHolds>();
        holds.Setup(x => x.ForMembersAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, ProbeEditionHold>());
        var access = new Mock<IResourceAccessService>();
        access.Setup(x => x.CheckAccessAsync(It.IsAny<ResourceType>(), It.IsAny<ResourceAction>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ResourceAccessDecision.Allow());

        var result = await new GetKurinProbeEditionsQueryHandler(_unitOfWork.Object, _catalog.Object, holds.Object, Mock.Of<IMemberDirectory>(), access.Object)
            .Handle(new GetKurinProbeEditionsQuery(_kurin.KurinKey), CancellationToken.None);

        result.Data!.Groups.Single().Edition.Should().BeNull();
    }
}
