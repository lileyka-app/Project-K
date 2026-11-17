using FluentAssertions;
using Moq;
using ProjectK.BusinessLogic.Modules.InfrastructureModule.Services;
using ProjectK.BusinessLogic.Tests.TestHelpers;
using ProjectK.Common.Contracts.KurinModule;
using ProjectK.Common.Contracts.Shared;
using ProjectK.Common.Dtos.MemberModule;
using ProjectK.Common.Entities.KurinModule;
using ProjectK.Common.Enums.AuthModule;
using ProjectK.Common.Enums.InfrastructureModule;
using ProjectK.Common.Enums.KurinModule;
using ProjectK.Common.Records.AuthModule;
using ProjectK.Common.Records.KurinModule;
using ProjectK.Common.Rules.AuthModule;

namespace ProjectK.BusinessLogic.Tests.InfrastructureModule.Notifications;

/// <summary>
/// Who hears about a submitted award or вмілість is who may act on it — under the kurin's own policy.
/// One kurin: a Зв'язковий, a впорядник mentoring the гурток, a Курінний, a Гуртковий of the гурток,
/// and a mentor assignment left over from someone who holds no office.
/// </summary>
public class ReviewNotificationRecipientResolverTests
{
    private readonly Guid _kurinKey = Guid.NewGuid();
    private readonly Guid _groupKey = Guid.NewGuid();
    private readonly Guid _zvyazkovyi = Guid.NewGuid();
    private readonly Guid _vykhovnyk = Guid.NewGuid();
    private readonly Guid _kurinnyy = Guid.NewGuid();
    private readonly Guid _hurtkovyy = Guid.NewGuid();
    private readonly Guid _hurtkovyyMember = Guid.NewGuid();
    private readonly Guid _officeless = Guid.NewGuid();
    private readonly Guid _revoked = Guid.NewGuid();

    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly Mock<ILeadershipRepository> _leaderships = new();
    private readonly Mock<IMentorAssignmentRepository> _mentors = new();
    private readonly Mock<IMembershipDirectory> _members = new();
    private readonly FixedKurinAccessPolicyProvider _policies = new();
    private readonly ReviewNotificationRecipientReader _resolver;

    public ReviewNotificationRecipientResolverTests()
    {
        _unitOfWork.SetupGet(x => x.Leaderships).Returns(_leaderships.Object);
        _unitOfWork.SetupGet(x => x.MentorAssignments).Returns(_mentors.Object);

        _leaderships
            .Setup(x => x.GetOfficeHoldersAsync(_kurinKey, LeadershipType.KV, It.IsAny<CancellationToken>()))
            .ReturnsAsync(
            [
                Holder(_zvyazkovyi, LeadershipType.KV, LeadershipRole.Zvyazkovyi),
                Holder(_vykhovnyk, LeadershipType.KV, LeadershipRole.Vykhovnyk)
            ]);
        _leaderships
            .Setup(x => x.GetOfficeHoldersAsync(_kurinKey, LeadershipType.Kurin, It.IsAny<CancellationToken>()))
            .ReturnsAsync([Holder(_kurinnyy, LeadershipType.Kurin, LeadershipRole.Kurinnuy)]);
        _leaderships
            .Setup(x => x.GetActiveOfficeMemberKeysAsync(
                It.Is<IReadOnlyCollection<LeadershipRole>>(roles => roles.Contains(LeadershipRole.Hurtkoviy)),
                null,
                _groupKey,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([_hurtkovyyMember]);
        _members
            .Setup(x => x.GetLookupByKurinAsync(_kurinKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync([new MemberLookupDto { MemberKey = _hurtkovyyMember, UserKey = _hurtkovyy }]);
        _mentors
            .Setup(x => x.GetActiveMentorsInKurinAsync(_kurinKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        _mentors
            .Setup(x => x.GetByGroupKeyAsync(_groupKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(
            [
                new MentorAssignment { GroupKey = _groupKey, MentorUserKey = _vykhovnyk },
                new MentorAssignment { GroupKey = _groupKey, MentorUserKey = _officeless },
                new MentorAssignment { GroupKey = _groupKey, MentorUserKey = _revoked, RevokedAtUtc = DateTime.UtcNow }
            ]);

        _resolver = new ReviewNotificationRecipientReader(_unitOfWork.Object, _members.Object, _policies);
    }

    private static MemberLookupRow Holder(Guid userKey, LeadershipType type, LeadershipRole role) =>
        new(Guid.NewGuid(), userKey, "Ім'я", null, "Прізвище", null, new MemberOffice(type, role));

    /// <summary>
    /// A виховник closed over the гурток but never seated in the КВ is its впорядник all the same, and
    /// is told like one; a revoked assignment is not read as a mentor at all.
    /// </summary>
    [Fact]
    public async Task AnUnseatedMentorOfTheGurtok_IsToldAsItsVporyadnyk()
    {
        _mentors
            .Setup(x => x.GetActiveMentorsInKurinAsync(_kurinKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync([new MemberLookupRow(Guid.NewGuid(), _officeless, "Ім'я", null, "Прізвище", null, null)]);

        var result = await _resolver.ResolveAsync(ReviewSubject.Award, _kurinKey, _groupKey, excludedUserKey: null);

        result.Should().BeEquivalentTo([_zvyazkovyi, _vykhovnyk, _officeless]);
    }

    [Theory]
    [InlineData(ReviewSubject.Award)]
    [InlineData(ReviewSubject.Badge)]
    [InlineData(ReviewSubject.Profile)]
    public async Task ByDefault_TheZvyazkovyiAndTheGurtoksVpornyadnyk_AreTold(ReviewSubject subject)
    {
        var result = await _resolver.ResolveAsync(subject, _kurinKey, _groupKey, excludedUserKey: null);

        result.Should().BeEquivalentTo([_zvyazkovyi, _vykhovnyk]);
    }

    [Fact]
    public async Task TheOneWhoSubmitted_IsNotTold()
    {
        var result = await _resolver.ResolveAsync(ReviewSubject.Award, _kurinKey, _groupKey, _zvyazkovyi);

        result.Should().BeEquivalentTo([_vykhovnyk]);
    }

    [Fact]
    public async Task WithoutAGurtok_OnlyWholeKurinReviewersAreTold()
    {
        var result = await _resolver.ResolveAsync(ReviewSubject.Award, _kurinKey, groupKey: null, excludedUserKey: null);

        result.Should().BeEquivalentTo([_zvyazkovyi]);
        _mentors.Verify(x => x.GetByGroupKeyAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task AKurinThatTakesBadgesFromVporyadnyky_StopsTellingThem()
    {
        _policies.Policy = PolicyAccess.With(
            (KurinCapability.ReviewBadge, PolicyAudience.Vykhovnyk, CapabilityReach.None),
            (KurinCapability.ReviewBadge, PolicyAudience.Instruktor, CapabilityReach.None));

        var badge = await _resolver.ResolveAsync(ReviewSubject.Badge, _kurinKey, _groupKey, excludedUserKey: null);
        var award = await _resolver.ResolveAsync(ReviewSubject.Award, _kurinKey, _groupKey, excludedUserKey: null);

        badge.Should().BeEquivalentTo([_zvyazkovyi]);
        award.Should().BeEquivalentTo([_zvyazkovyi, _vykhovnyk]);
    }

    [Fact]
    public async Task AKurinThatGivesAwardsToTheHurtkovyy_TellsTheGurtoksOne()
    {
        _policies.Policy = PolicyAccess.With((KurinCapability.ConfirmAward1, PolicyAudience.Hurtkovyy, CapabilityReach.OwnGroup));

        var result = await _resolver.ResolveAsync(ReviewSubject.Award, _kurinKey, _groupKey, excludedUserKey: null);

        result.Should().BeEquivalentTo([_zvyazkovyi, _vykhovnyk, _hurtkovyy]);
    }

    [Fact]
    public async Task AKurinThatGivesAwardsToTheKurinnyy_TellsHimForEveryGurtok()
    {
        _policies.Policy = PolicyAccess.With((KurinCapability.ConfirmAward1, PolicyAudience.Kurinnyy, CapabilityReach.Kurin));

        var result = await _resolver.ResolveAsync(ReviewSubject.Award, _kurinKey, groupKey: null, excludedUserKey: null);

        result.Should().BeEquivalentTo([_zvyazkovyi, _kurinnyy]);
    }
}
