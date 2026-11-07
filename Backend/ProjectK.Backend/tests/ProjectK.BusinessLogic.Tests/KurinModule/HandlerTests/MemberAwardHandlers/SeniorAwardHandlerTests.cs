using AutoMapper;
using FluentAssertions;
using Moq;
using ProjectK.BusinessLogic.Modules.KurinModule.Features.MemberAward.Delete;
using ProjectK.BusinessLogic.Modules.KurinModule.Features.MemberAward.Review;
using ProjectK.BusinessLogic.Modules.KurinModule.Features.MemberAward.Upsert;
using ProjectK.BusinessLogic.Tests.TestHelpers;
using ProjectK.Common.Entities.KurinModule;
using ProjectK.Common.Interfaces;
using ProjectK.Common.Interfaces.Modules.InfrastructureModule;
using ProjectK.Common.Interfaces.Modules.KurinModule;
using ProjectK.Common.Interfaces.Modules.MemberModule;
using ProjectK.Common.Interfaces.Modules.ProbesAndBadgesModule;
using ProjectK.Common.Models.Authorization.KurinPolicy;
using ProjectK.Common.Models.Catalog;
using ProjectK.Common.Models.Dtos.InfrastructureModule;
using ProjectK.Common.Models.Dtos.KurinModule;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Events;
using ProjectK.Common.Models.Records;
using Xunit;

namespace ProjectK.BusinessLogic.Tests.KurinModule.HandlerTests.MemberAwardHandlers;

/// <summary>
/// A senior відзначення is the catalogue's, recorded in a УСП or УПС kurin by the person or its провід,
/// and it stands as recorded: nobody confirms it. An УПЮ kurin keeps its levels 1–4.
/// </summary>
public class SeniorAwardHandlerTests
{
    private readonly Mock<IMemberUnitOfWork> _unitOfWork = new();
    private readonly Mock<IMemberRepository> _members = new();
    private readonly Mock<IMemberAwardRepository> _awards = new();
    private readonly Mock<ICurrentUserContext> _currentUser = new();
    private readonly Mock<IDomainEventPublisher> _events = new();
    private readonly Mock<IMapper> _mapper = new();
    private readonly Mock<IUnitOfWork> _kurinData = new();
    private readonly Mock<IMembershipRepository> _memberships = new();
    private readonly Mock<IKurinRepository> _kurins = new();
    private readonly Mock<IHonoursCatalog> _honours = new();
    private readonly Mock<IMemberDirectory> _directory = new();
    private readonly Mock<IResourceAccessService> _access = new();
    private readonly Mock<IResourceScopeReader> _scope = new();
    private readonly Guid _kurinKey = Guid.NewGuid();
    private readonly Guid _ownerUserKey = Guid.NewGuid();
    private readonly Member _member;
    private readonly List<MemberAward> _stored = [];
    private JournalRecorder _journal = null!;

    public SeniorAwardHandlerTests()
    {
        _member = new Member { MemberKey = Guid.NewGuid(), UserKey = _ownerUserKey, FirstName = "Ірина", LastName = "Федорів" };
        _unitOfWork.SetupGet(u => u.Members).Returns(_members.Object);
        _unitOfWork.SetupGet(u => u.MemberAwards).Returns(_awards.Object);
        _members.Setup(m => m.GetByKeyAsync(_member.MemberKey, It.IsAny<CancellationToken>())).ReturnsAsync(_member);
        _awards.Setup(a => a.Create(It.IsAny<MemberAward>(), It.IsAny<CancellationToken>()))
            .Callback((MemberAward award, CancellationToken _) => _stored.Add(award));
        _awards.Setup(a => a.GetByKeyAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid key, CancellationToken _) => _stored.FirstOrDefault(a => a.MemberAwardKey == key));
        _mapper.Setup(m => m.Map<MemberAwardDto>(It.IsAny<MemberAward>())).Returns(new MemberAwardDto());
        _currentUser.SetupGet(c => c.KurinKey).Returns(_kurinKey);
        _currentUser.SetupGet(c => c.UserId).Returns(_ownerUserKey);
        _kurinData.SetupGet(k => k.Memberships).Returns(_memberships.Object);
        _kurinData.SetupGet(k => k.Kurins).Returns(_kurins.Object);
        _memberships.Setup(m => m.GetActiveForMemberAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync([]);
        _directory.Setup(d => d.FindAccountKeyAsync(_member.MemberKey, It.IsAny<CancellationToken>())).ReturnsAsync(_ownerUserKey);
        _honours.Setup(h => h.GetById("orden-sv-yuriia-bronza"))
            .Returns(new CatalogHonour("orden-sv-yuriia-bronza", HonourKind.Award, "Орден святого Юрія в бронзі", "orden-sv-yuriia", "usp", 0, ""));
        _honours.Setup(h => h.GetById("usp-starshyi-plastun"))
            .Returns(new CatalogHonour("usp-starshyi-plastun", HonourKind.Degree, "Старший пластун", "usp", "usp", 1, ""));
        _access.Setup(a => a.CheckCapabilityAsync(It.IsAny<KurinCapability>(), It.IsAny<ResourceType>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ResourceAccessDecision.Allow());
        InKurin(KurinBranch.USP);
    }

    private void InKurin(KurinBranch branch) =>
        _kurins.Setup(k => k.GetByKeyAsync(_kurinKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Kurin.InBranches([new KurinNumbering(branch, 5)]));

    private void ActAs(Guid userKey) => _currentUser.SetupGet(c => c.UserId).Returns(userKey);

    private void Refuse(KurinCapability capability) =>
        _access.Setup(a => a.CheckCapabilityAsync(capability, It.IsAny<ResourceType>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ResourceAccessDecision.Deny("Refused by the test."));

    private UpsertMemberAwardCommandHandler Upsert() => new(
        _unitOfWork.Object, _currentUser.Object, _events.Object, _mapper.Object, _kurinData.Object, _access.Object,
        (_journal = new JournalRecorder(_currentUser.Object)).Audit, _honours.Object, _scope.Object);

    private DeleteMemberAwardCommandHandler Delete() => new(
        _unitOfWork.Object, _currentUser.Object, _directory.Object, _access.Object,
        (_journal = new JournalRecorder(_currentUser.Object)).Audit, _honours.Object, _kurinData.Object);

    private Task<Common.Models.Records.ServiceResult<MemberAwardDto>> Record(string? catalogId, MemberAwardLevel? level = null) =>
        Upsert().Handle(new UpsertMemberAwardCommand
        {
            MemberKey = _member.MemberKey,
            CatalogId = catalogId,
            Level = level,
            DateAcquired = new DateTime(2018, 4, 23)
        }, CancellationToken.None);

    [Fact]
    public async Task TheirOwnSeniorAward_StandsAtOnce_AndAsksNobodyToConfirmIt()
    {
        var result = await Record("orden-sv-yuriia-bronza");

        result.Type.Should().Be(ResultType.Success);
        var award = _stored.Should().ContainSingle().Subject;
        award.CatalogId.Should().Be("orden-sv-yuriia-bronza");
        award.Level.Should().BeNull();
        award.Status.Should().Be(BadgeProgressStatus.Confirmed);
        _events.Verify(e => e.PublishAsync(It.IsAny<MemberAwardSubmitted>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task EachUlad_ItsOwnAwards()
    {
        InKurin(KurinBranch.UPYu);
        (await Record("orden-sv-yuriia-bronza")).Type.Should().Be(ResultType.BadRequest);

        InKurin(KurinBranch.UPS);
        (await Record(null, MemberAwardLevel.First)).Type.Should().Be(ResultType.BadRequest);
        _stored.Should().BeEmpty();
    }

    [Fact]
    public async Task OnlyAnAwardFromTheCatalogue_NotAStupin()
    {
        (await Record("usp-starshyi-plastun")).Type.Should().Be(ResultType.BadRequest);
        (await Record("nema-takoho")).Type.Should().Be(ResultType.BadRequest);
        _stored.Should().BeEmpty();
    }

    [Fact]
    public async Task ThePersonTakesTheirOwnBack_SomeoneElseNeedsTheRightToRemove()
    {
        await Record("orden-sv-yuriia-bronza");
        var award = _stored.Single();
        Refuse(KurinCapability.RemoveAward);

        ActAs(Guid.NewGuid());
        (await Delete().Handle(new DeleteMemberAwardCommand { MemberKey = _member.MemberKey, MemberAwardKey = award.MemberAwardKey }, CancellationToken.None))
            .Type.Should().Be(ResultType.Forbidden);

        ActAs(_ownerUserKey);
        (await Delete().Handle(new DeleteMemberAwardCommand { MemberKey = _member.MemberKey, MemberAwardKey = award.MemberAwardKey }, CancellationToken.None))
            .Type.Should().Be(ResultType.Success);
        _awards.Verify(a => a.Delete(award, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SomeoneElsesSeniorAward_IsRemovedOnlyFromASeniorKurin()
    {
        await Record("orden-sv-yuriia-bronza");
        var award = _stored.Single();
        InKurin(KurinBranch.UPYu);
        ActAs(Guid.NewGuid());

        var refused = await Delete().Handle(new DeleteMemberAwardCommand { MemberKey = _member.MemberKey, MemberAwardKey = award.MemberAwardKey }, CancellationToken.None);

        refused.Type.Should().Be(ResultType.BadRequest);
        refused.ErrorCode.Should().Be("SeniorAwardOutsideSeniorKurin");
        _awards.Verify(a => a.Delete(It.IsAny<MemberAward>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ASeniorWhoLeadsAnUpyuKurin_KeepsTheirOwnSeniorAwards_ThereToo()
    {
        InKurin(KurinBranch.UPYu);
        // The скоб of 2002 is on record, but the current ступінь is the сеніор's.
        _member.PlastLevelHistory.Add(new PlastLevelHistory { MemberKey = _member.MemberKey, PlastLevel = PlastLevel.Skob, DateAchieved = new DateOnly(2002, 6, 1) });
        _member.PlastLevelHistory.Add(new PlastLevelHistory { MemberKey = _member.MemberKey, PlastLevel = PlastLevel.Senior, DateAchieved = new DateOnly(2015, 4, 26) });

        (await Record("orden-sv-yuriia-bronza")).Type.Should().Be(ResultType.Success);
        var award = _stored.Single();
        award.Status.Should().Be(BadgeProgressStatus.Confirmed);

        (await Delete().Handle(new DeleteMemberAwardCommand { MemberKey = _member.MemberKey, MemberAwardKey = award.MemberAwardKey }, CancellationToken.None))
            .Type.Should().Be(ResultType.Success);
    }

    [Fact]
    public async Task AnUpyuKurinsProvid_RecordsNobodyElsesSeniorAward_EvenASeniors()
    {
        InKurin(KurinBranch.UPYu);
        _member.PlastLevelHistory.Add(new PlastLevelHistory { MemberKey = _member.MemberKey, PlastLevel = PlastLevel.Senior });
        ActAs(Guid.NewGuid());

        (await Record("orden-sv-yuriia-bronza")).Type.Should().Be(ResultType.BadRequest);
        _stored.Should().BeEmpty();
    }

    [Fact]
    public async Task AYouthOnTheirOwnCard_RecordsNoSeniorAward_UnlessTheyStandInASeniorKurin()
    {
        InKurin(KurinBranch.UPYu);
        _member.PlastLevelHistory.Add(new PlastLevelHistory { MemberKey = _member.MemberKey, PlastLevel = PlastLevel.Skob });

        (await Record("orden-sv-yuriia-bronza")).Type.Should().Be(ResultType.BadRequest);

        _scope.Setup(s => s.StandsInSeniorKurinAsync(ResourceType.Member, _member.MemberKey, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        (await Record("orden-sv-yuriia-bronza")).Type.Should().Be(ResultType.Success);
    }

    [Fact]
    public async Task TheJournal_NamesTheSeniorAwardByItsCatalogueTitle()
    {
        _honours.Setup(h => h.GetById("orden-sv-yuriia-sribla"))
            .Returns(new CatalogHonour("orden-sv-yuriia-sribla", HonourKind.Award, "Орден святого Юрія в сріблі", "orden-sv-yuriia", "usp", 1, ""));
        await Record("orden-sv-yuriia-bronza");
        _journal.Entries.Single().Changes.Should().Contain(new Common.Models.Audit.AuditChange("award", null, "Орден святого Юрія в бронзі"));

        await Upsert().Handle(new UpsertMemberAwardCommand
        {
            MemberKey = _member.MemberKey,
            MemberAwardKey = _stored.Single().MemberAwardKey,
            CatalogId = "orden-sv-yuriia-sribla",
            DateAcquired = new DateTime(2018, 4, 23)
        }, CancellationToken.None);

        _journal.Entries.Single().Changes.Should().Equal(
            new Common.Models.Audit.AuditChange("award", "Орден святого Юрія в бронзі", "Орден святого Юрія в сріблі"));
        _journal.Entries.SelectMany(e => e.Changes).Should().NotContain(c => c.Field == "catalogId");
    }

    [Fact]
    public async Task SomeoneElseChangesIt_OnlyWithTheRightToRemove()
    {
        await Record("orden-sv-yuriia-bronza");
        var award = _stored.Single();
        Refuse(KurinCapability.RemoveAward);
        ActAs(Guid.NewGuid());

        var change = await Upsert().Handle(new UpsertMemberAwardCommand
        {
            MemberKey = _member.MemberKey,
            MemberAwardKey = award.MemberAwardKey,
            CatalogId = "orden-sv-yuriia-bronza",
            DateAcquired = new DateTime(2019, 1, 1)
        }, CancellationToken.None);

        change.Type.Should().Be(ResultType.Forbidden);
        award.DateAcquired.Should().Be(new DateTime(2018, 4, 23));
    }

    [Fact]
    public async Task ASeniorAward_IsNotReviewed()
    {
        await Record("orden-sv-yuriia-bronza");
        var review = new ReviewMemberAwardCommandHandler(
            _unitOfWork.Object, _directory.Object, _currentUser.Object, _events.Object, _mapper.Object, _access.Object,
            new JournalRecorder(_currentUser.Object).Audit);

        var result = await review.Handle(
            new ReviewMemberAwardCommand { MemberKey = _member.MemberKey, MemberAwardKey = _stored.Single().MemberAwardKey, IsApproved = true },
            CancellationToken.None);

        result.Type.Should().Be(ResultType.Conflict);
    }

    [Fact]
    public void AnAward_IsEitherALevelOrACatalogueOne()
    {
        var validator = new UpsertMemberAwardCommandValidator(TimeProvider.System);
        UpsertMemberAwardCommand Command(MemberAwardLevel? level, string? catalogId) =>
            new() { MemberKey = Guid.NewGuid(), Level = level, CatalogId = catalogId, DateAcquired = new DateTime(2020, 1, 1) };

        validator.Validate(Command(MemberAwardLevel.First, null)).IsValid.Should().BeTrue();
        validator.Validate(Command(null, "orden-sv-yuriia-bronza")).IsValid.Should().BeTrue();
        validator.Validate(Command(MemberAwardLevel.First, "orden-sv-yuriia-bronza")).IsValid.Should().BeFalse();
        validator.Validate(Command(null, null)).IsValid.Should().BeFalse();
    }

    [Fact]
    public void ASeniorAward_IsReadWithItsCatalogueId_AndNoLevelPicture()
    {
        var mapper = new MapperConfiguration(cfg =>
        {
            cfg.ConstructServicesUsing(FakeBlobReadLinks.Resolvers);
            cfg.AddProfile(new ProjectK.BusinessLogic.MappingProfiles.KurinModuleProfile());
        }, Microsoft.Extensions.Logging.LoggerFactory.Create(_ => { })).CreateMapper();

        var dto = mapper.Map<MemberAwardDto>(new MemberAward { CatalogId = "orden-sv-yuriia-bronza", Status = BadgeProgressStatus.Confirmed });

        dto.CatalogId.Should().Be("orden-sv-yuriia-bronza");
        dto.Level.Should().BeNull();
        dto.ImageUrl.Should().BeNull();
    }

    [Fact]
    public async Task ThePersonPinsAnAward_EveryGrantOfIt_AndOnlyTheirOwn()
    {
        var other = new MemberAward { MemberKey = _member.MemberKey, Level = MemberAwardLevel.First };
        _stored.Add(new MemberAward { MemberKey = _member.MemberKey, CatalogId = "usp-hramota-podiaky" });
        _stored.Add(new MemberAward { MemberKey = _member.MemberKey, CatalogId = "usp-hramota-podiaky" });
        _stored.Add(other);
        _members.Setup(m => m.GetSummaryByUserKeyAsync(_ownerUserKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MemberSummary(_member.MemberKey, _ownerUserKey, _kurinKey, null, "Ірина", "Федорів", "i@example.com", null));
        _awards.Setup(a => a.GetByMemberKeyAsync(_member.MemberKey, It.IsAny<CancellationToken>())).ReturnsAsync(() => _stored.ToList());
        var pin = new ProjectK.BusinessLogic.Modules.KurinModule.Features.MemberAward.Pin.PinMyAwardCommandHandler(_unitOfWork.Object, _currentUser.Object);

        var result = await pin.Handle(new ProjectK.BusinessLogic.Modules.KurinModule.Features.MemberAward.Pin.PinMyAwardCommand { CatalogId = "usp-hramota-podiaky", Pinned = true }, CancellationToken.None);

        result.Type.Should().Be(ResultType.Success);
        _stored.Where(a => a.CatalogId == "usp-hramota-podiaky").Should().OnlyContain(a => a.IsPinned);
        other.IsPinned.Should().BeFalse();

        (await pin.Handle(new ProjectK.BusinessLogic.Modules.KurinModule.Features.MemberAward.Pin.PinMyAwardCommand { CatalogId = "nema", Pinned = true }, CancellationToken.None))
            .Type.Should().Be(ResultType.NotFound);
    }
}
