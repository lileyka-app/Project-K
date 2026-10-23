using AutoMapper;
using FluentAssertions;
using Moq;
using ProjectK.BusinessLogic.Modules.KurinModule.Features.Member.Abilities;
using ProjectK.BusinessLogic.Modules.KurinModule.Features.MemberAward.Review;
using ProjectK.BusinessLogic.Modules.KurinModule.Features.MemberWarning.Assign;
using ProjectK.BusinessLogic.Modules.KurinModule.Features.MemberWarning.Cancel;
using ProjectK.BusinessLogic.Modules.ProbesAndBadgesModule.Features.Badge.Get;
using ProjectK.BusinessLogic.Tests.TestHelpers;
using ProjectK.Common.Entities.KurinModule;
using ProjectK.Common.Entities.ProbesAndBadgesModule;
using ProjectK.Common.Interfaces;
using ProjectK.Common.Interfaces.Modules.InfrastructureModule;
using ProjectK.Common.Interfaces.Modules.KurinModule;
using ProjectK.Common.Interfaces.Modules.MemberModule;
using ProjectK.Common.Interfaces.Modules.ProbesAndBadgesModule;
using ProjectK.Common.Models.Authorization.KurinPolicy;
using ProjectK.Common.Models.Dtos.KurinModule;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Records;

namespace ProjectK.BusinessLogic.Tests.AuthModule;

/// <summary>
/// The handlers that check a level or a rule beyond the endpoint, run against the real decision:
/// roles folded into audiences, the kurin's policy, the member's гурток. The default policy is
/// today's behaviour; each override is one cell a kurin could set.
/// </summary>
public class KurinPolicyEnforcementTests
{
    private readonly PolicyAccess _access = new();
    private readonly Mock<IMemberUnitOfWork> _unitOfWork = new();
    private readonly Mock<IMemberRepository> _memberRepository = new();
    private readonly Mock<IMemberWarningRepository> _warnings = new();
    private readonly Mock<IMemberAwardRepository> _awards = new();
    private readonly FixedTimeProvider _clock = new(new DateTimeOffset(2026, 10, 23, 9, 0, 0, TimeSpan.Zero));

    public KurinPolicyEnforcementTests()
    {
        _unitOfWork.SetupGet(x => x.Members).Returns(_memberRepository.Object);
        _unitOfWork.SetupGet(x => x.MemberWarnings).Returns(_warnings.Object);
        _unitOfWork.SetupGet(x => x.MemberAwards).Returns(_awards.Object);
        _unitOfWork.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
        _memberRepository
            .Setup(x => x.GetByKeyAsync(_access.MemberKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Member { MemberKey = _access.MemberKey });
    }

    private Task<ServiceResult<MemberWarningDto>> AssignAsync(ICurrentUserContext actor, MemberWarningLevel level, params MemberWarningLevel[] active)
    {
        _warnings
            .Setup(x => x.GetActiveByMemberKeyAsync(_access.MemberKey, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(active.Select(held => new MemberWarning { MemberKey = _access.MemberKey, Level = held }).ToList());
        var handler = new AssignMemberWarningCommandHandler(
            _unitOfWork.Object, actor, Mock.Of<IDomainEventPublisher>(), Mock.Of<IMapper>(), _access.ServiceFor(actor));
        return handler.Handle(new AssignMemberWarningCommand(_access.MemberKey, level), CancellationToken.None);
    }

    private Task<ServiceResult<MemberWarningDto>> CancelAsync(ICurrentUserContext actor, Guid issuedBy, MemberWarningLevel level)
    {
        var now = _clock.GetUtcNow().UtcDateTime;
        var warning = new MemberWarning
        {
            MemberWarningKey = Guid.NewGuid(),
            MemberKey = _access.MemberKey,
            Level = level,
            IssuedAtUtc = now.AddDays(-5),
            ExpiresAtUtc = now.AddDays(30),
            IssuedByUserKey = issuedBy
        };
        _warnings.Setup(x => x.GetByKeyAsync(warning.MemberWarningKey, It.IsAny<CancellationToken>())).ReturnsAsync(warning);
        var handler = new CancelMemberWarningCommandHandler(_unitOfWork.Object, actor, Mock.Of<IMapper>(), _clock, _access.ServiceFor(actor));
        return handler.Handle(new CancelMemberWarningCommand(_access.MemberKey, warning.MemberWarningKey), CancellationToken.None);
    }

    private Task<ServiceResult<MemberAwardDto>> ReviewAsync(ICurrentUserContext actor, MemberAwardLevel level)
    {
        var award = new MemberAward
        {
            MemberAwardKey = Guid.NewGuid(),
            MemberKey = _access.MemberKey,
            Level = level,
            Status = BadgeProgressStatus.Submitted
        };
        _awards.Setup(x => x.GetByKeyAsync(award.MemberAwardKey, It.IsAny<CancellationToken>())).ReturnsAsync(award);
        var handler = new ReviewMemberAwardCommandHandler(
            _unitOfWork.Object, Mock.Of<IMemberDirectory>(), actor, Mock.Of<IDomainEventPublisher>(), Mock.Of<IMapper>(), _access.ServiceFor(actor));
        return handler.Handle(
            new ReviewMemberAwardCommand { MemberKey = _access.MemberKey, MemberAwardKey = award.MemberAwardKey, IsApproved = true },
            CancellationToken.None);
    }

    [Theory]
    [InlineData(MemberWarningLevel.Level1)]
    [InlineData(MemberWarningLevel.Level2, MemberWarningLevel.Level1)]
    [InlineData(MemberWarningLevel.Level3, MemberWarningLevel.Level1, MemberWarningLevel.Level2)]
    public async Task ByDefault_TheVporyadnykGivesEveryLevel_InHisGurtok(MemberWarningLevel level, params MemberWarningLevel[] active)
    {
        var result = await AssignAsync(_access.Actor(PolicyAccess.Vykhovnyk), level, active);

        result.Type.Should().Be(ResultType.Created);
    }

    [Fact]
    public async Task AKurinThatKeepsTheThirdWarning_LetsTheVporyadnykGiveOnlyTheFirstTwo()
    {
        _access.Policies.Policy = PolicyAccess.With((KurinCapability.GiveWarning3, PolicyAudience.Vykhovnyk, CapabilityReach.None));
        var actor = _access.Actor(PolicyAccess.Vykhovnyk);

        var first = await AssignAsync(actor, MemberWarningLevel.Level1);
        var third = await AssignAsync(actor, MemberWarningLevel.Level3, MemberWarningLevel.Level1, MemberWarningLevel.Level2);

        first.Type.Should().Be(ResultType.Created);
        third.Type.Should().Be(ResultType.Forbidden);
        third.ErrorCode.Should().Be("WarningLevelNotAllowed");
    }

    [Fact]
    public async Task AVporyadnyk_GivesNoWarning_OutsideHisGurtok()
    {
        _access.LedGroups.Clear();
        _access.LedGroups.Add(_access.OtherGroupKey);

        var result = await AssignAsync(_access.Actor(PolicyAccess.Vykhovnyk), MemberWarningLevel.Level1);

        result.Type.Should().Be(ResultType.Forbidden);
    }

    [Fact]
    public async Task AKurinCanGiveWarningsToTheHurtkovyy()
    {
        var actor = _access.Actor(PolicyAccess.Hurtkovyy);
        var before = await AssignAsync(actor, MemberWarningLevel.Level1);

        _access.Policies.Policy = PolicyAccess.With((KurinCapability.GiveWarning1, PolicyAudience.Hurtkovyy, CapabilityReach.OwnGroup));
        var after = await AssignAsync(actor, MemberWarningLevel.Level1);

        before.Type.Should().Be(ResultType.Forbidden);
        after.Type.Should().Be(ResultType.Created);
    }

    [Fact]
    public async Task TheIssuer_CancelsHisOwnWarning()
    {
        var result = await CancelAsync(_access.Actor(PolicyAccess.Vykhovnyk), _access.ActorUserKey, MemberWarningLevel.Level2);

        result.Type.Should().Be(ResultType.Success);
    }

    [Fact]
    public async Task TheIssuer_NoLongerCancels_ALevelTheKurinTookFromHim()
    {
        _access.Policies.Policy = PolicyAccess.With((KurinCapability.GiveWarning2, PolicyAudience.Vykhovnyk, CapabilityReach.None));

        var result = await CancelAsync(_access.Actor(PolicyAccess.Vykhovnyk), _access.ActorUserKey, MemberWarningLevel.Level2);

        result.Type.Should().Be(ResultType.Forbidden);
    }

    [Fact]
    public async Task AVporyadnyk_DoesNotCancelSomeoneElsesWarning()
    {
        var result = await CancelAsync(_access.Actor(PolicyAccess.Vykhovnyk), Guid.NewGuid(), MemberWarningLevel.Level1);

        result.Type.Should().Be(ResultType.Forbidden);
    }

    [Fact]
    public async Task TheZvyazkovyi_CancelsAnyonesWarning()
    {
        _access.LedGroups.Clear();

        var result = await CancelAsync(_access.Actor(PolicyAccess.Zvyazkovyi), Guid.NewGuid(), MemberWarningLevel.Level3);

        result.Type.Should().Be(ResultType.Success);
    }

    [Fact]
    public async Task AKurinCanLetVporyadnykyCancelAnyonesWarning_InTheirGurtok()
    {
        _access.Policies.Policy = PolicyAccess.With((KurinCapability.CancelWarning, PolicyAudience.Vykhovnyk, CapabilityReach.OwnGroup));

        var result = await CancelAsync(_access.Actor(PolicyAccess.Vykhovnyk), Guid.NewGuid(), MemberWarningLevel.Level1);

        result.Type.Should().Be(ResultType.Success);
    }

    [Fact]
    public async Task AKurinThatKeepsTheFourthAward_LetsTheVporyadnykConfirmOnlyTheOthers()
    {
        _access.Policies.Policy = PolicyAccess.With((KurinCapability.ConfirmAward4, PolicyAudience.Vykhovnyk, CapabilityReach.None));
        var actor = _access.Actor(PolicyAccess.Vykhovnyk);

        var third = await ReviewAsync(actor, MemberAwardLevel.Third);
        var fourth = await ReviewAsync(actor, MemberAwardLevel.Fourth);

        third.Type.Should().Be(ResultType.Success);
        fourth.Type.Should().Be(ResultType.Forbidden);
        fourth.ErrorCode.Should().Be("AwardLevelNotAllowed");
    }

    [Fact]
    public async Task TheZvyazkovyi_ConfirmsEveryLevel_WhateverTheKurinSetForOthers()
    {
        _access.Policies.Policy = PolicyAccess.With(
            (KurinCapability.ConfirmAward4, PolicyAudience.Vykhovnyk, CapabilityReach.None),
            (KurinCapability.ConfirmAward4, PolicyAudience.Instruktor, CapabilityReach.None));
        _access.LedGroups.Clear();

        var result = await ReviewAsync(_access.Actor(PolicyAccess.Zvyazkovyi), MemberAwardLevel.Fourth);

        result.Type.Should().Be(ResultType.Success);
    }

    private async Task<MemberAbilitiesDto> AbilitiesAsync(ICurrentUserContext actor)
    {
        var result = await new GetMemberAbilitiesQueryHandler(_access.ServiceFor(actor))
            .Handle(new GetMemberAbilitiesQuery(_access.MemberKey), CancellationToken.None);
        result.Type.Should().Be(ResultType.Success);
        return result.Data!;
    }

    [Fact]
    public async Task Abilities_OfTheZvyazkovyi_AreEverything()
    {
        _access.LedGroups.Clear();

        var abilities = await AbilitiesAsync(_access.Actor(PolicyAccess.Zvyazkovyi));

        abilities.Should().BeEquivalentTo(new MemberAbilitiesDto(
            new WarningAbilitiesDto([1, 2, 3], WarningCancelAbility.Any),
            new AwardAbilitiesDto([1, 2, 3, 4], Remove: true),
            new ProbeAbilitiesDto(Sign: true, Unsign: true),
            new BadgeAbilitiesDto(Review: true),
            Journal: true));
    }

    [Fact]
    public async Task Abilities_OfTheVporyadnyk_InHisGurtok_FollowTheDefaults()
    {
        var abilities = await AbilitiesAsync(_access.Actor(PolicyAccess.Vykhovnyk));

        abilities.Should().BeEquivalentTo(new MemberAbilitiesDto(
            new WarningAbilitiesDto([1, 2, 3], WarningCancelAbility.OwnIssued),
            new AwardAbilitiesDto([1, 2, 3, 4], Remove: false),
            new ProbeAbilitiesDto(Sign: true, Unsign: true),
            new BadgeAbilitiesDto(Review: true),
            Journal: true));
    }

    [Fact]
    public async Task Abilities_OfTheVporyadnyk_FollowTheKurinsLevels()
    {
        _access.Policies.Policy = PolicyAccess.With(
            (KurinCapability.GiveWarning3, PolicyAudience.Vykhovnyk, CapabilityReach.None),
            (KurinCapability.ConfirmAward3, PolicyAudience.Vykhovnyk, CapabilityReach.None),
            (KurinCapability.ConfirmAward4, PolicyAudience.Vykhovnyk, CapabilityReach.None),
            (KurinCapability.UnsignProbe, PolicyAudience.Vykhovnyk, CapabilityReach.None));

        var abilities = await AbilitiesAsync(_access.Actor(PolicyAccess.Vykhovnyk));

        abilities.Warnings.Give.Should().Equal(1, 2);
        abilities.Awards.Confirm.Should().Equal(1, 2);
        abilities.Probes.Should().Be(new ProbeAbilitiesDto(Sign: true, Unsign: false));
    }

    [Fact]
    public async Task Abilities_OfTheVporyadnyk_OutsideHisGurtok_AreOnlyTheKurinWideOnes()
    {
        _access.LedGroups.Clear();
        _access.LedGroups.Add(_access.OtherGroupKey);

        var abilities = await AbilitiesAsync(_access.Actor(PolicyAccess.Vykhovnyk));

        abilities.Warnings.Give.Should().BeEmpty();
        abilities.Warnings.Cancel.Should().Be(WarningCancelAbility.None);
        abilities.Awards.Confirm.Should().BeEmpty();
        abilities.Probes.Should().Be(new ProbeAbilitiesDto(Sign: false, Unsign: false));
        abilities.Journal.Should().BeTrue();
    }

    [Fact]
    public async Task Abilities_OfAYouth_AreNone()
    {
        _access.LedGroups.Clear();

        var abilities = await AbilitiesAsync(_access.Actor());

        abilities.Should().BeEquivalentTo(new MemberAbilitiesDto(
            new WarningAbilitiesDto([], WarningCancelAbility.None),
            new AwardAbilitiesDto([], Remove: false),
            new ProbeAbilitiesDto(Sign: false, Unsign: false),
            new BadgeAbilitiesDto(Review: false),
            Journal: false));
    }

    private async Task<ServiceResult<IEnumerable<ProjectK.BusinessLogic.Modules.ProbesAndBadgesModule.Models.BadgeProgressResponse>>> QueueAsync(ICurrentUserContext actor)
    {
        var inGroup = Guid.NewGuid();
        var inOtherGroup = Guid.NewGuid();
        var members = new Mock<IMemberDirectory>();
        members.Setup(x => x.GetByKurinAsync(_access.KurinKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(
            [
                new MemberSummary(inGroup, null, _access.KurinKey, _access.GroupKey, "Ivan", "Led", "", null),
                new MemberSummary(inOtherGroup, null, _access.KurinKey, _access.OtherGroupKey, "Petro", "Other", "", null)
            ]);
        var progresses = new Mock<IBadgeProgressRepository>();
        progresses.Setup(x => x.GetByMemberKeysAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IEnumerable<Guid> keys, CancellationToken _) => keys
                .Select(key => new BadgeProgress { MemberKey = key, BadgeId = "badge", Status = BadgeProgressStatus.Submitted, SubmittedAtUtc = DateTime.UtcNow })
                .ToList());
        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork.SetupGet(x => x.BadgeProgresses).Returns(progresses.Object);
        var scopes = new Mock<IResourceScopeReader>();
        scopes.Setup(x => x.GetLedGroupKeysAsync(It.IsAny<Guid>(), _access.KurinKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => _access.LedGroups.ToArray());

        var handler = new GetBadgeReviewQueueQueryHandler(
            unitOfWork.Object, members.Object, actor, scopes.Object, FakeBlobReadLinks.Instance, _access.PermissionsFor(actor));
        return await handler.Handle(new GetBadgeReviewQueueQuery(_access.KurinKey), CancellationToken.None);
    }

    [Fact]
    public async Task ReviewQueue_OfTheZvyazkovyi_HoldsTheWholeKurin()
    {
        var result = await QueueAsync(_access.Actor(PolicyAccess.Zvyazkovyi));

        result.Data.Should().HaveCount(2);
    }

    [Fact]
    public async Task ReviewQueue_OfTheVporyadnyk_HoldsHisGurtokOnly()
    {
        var result = await QueueAsync(_access.Actor(PolicyAccess.Vykhovnyk));

        result.Data!.Select(item => item.MemberKey).Should().ContainSingle();
    }

    [Fact]
    public async Task ReviewQueue_OfSomeoneTheKurinDoesNotLetReview_IsRefused()
    {
        _access.Policies.Policy = PolicyAccess.With((KurinCapability.ReviewBadge, PolicyAudience.Vykhovnyk, CapabilityReach.None));

        var result = await QueueAsync(_access.Actor(PolicyAccess.Vykhovnyk));

        result.Type.Should().Be(ResultType.Forbidden);
        result.ErrorCode.Should().Be("BadgeReviewNotAllowed");
    }

    [Fact]
    public async Task ReviewQueue_OfAHurtkovyy_TheKurinLetsReviewKurinWide_HoldsTheWholeKurin()
    {
        _access.Policies.Policy = PolicyAccess.With((KurinCapability.ReviewBadge, PolicyAudience.Hurtkovyy, CapabilityReach.Kurin));

        var result = await QueueAsync(_access.Actor(PolicyAccess.Hurtkovyy));

        result.Data.Should().HaveCount(2);
    }
}
