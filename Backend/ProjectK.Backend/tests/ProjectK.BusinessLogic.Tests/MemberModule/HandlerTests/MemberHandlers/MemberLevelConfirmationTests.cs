using FluentAssertions;
using Moq;
using ProjectK.BusinessLogic.Modules.MemberModule.Features.Level;
using ProjectK.BusinessLogic.Modules.MemberModule.Services;
using ProjectK.BusinessLogic.Tests.TestHelpers;
using ProjectK.Common.Contracts.IdentityModule;
using ProjectK.Common.Contracts.MemberModule;
using ProjectK.Common.Contracts.Shared;
using ProjectK.Common.Entities.MemberModule;
using ProjectK.Common.Enums.IdentityModule;
using ProjectK.Common.Enums.Shared;
using ProjectK.Common.Records.IdentityModule;
using ProjectK.Common.Rules.AuditModule;

namespace ProjectK.BusinessLogic.Tests.MemberModule.HandlerTests.MemberHandlers;

/// <summary>
/// A waiting senior ступінь is answered by the Звʼязковий of a kurin the person stands in now — the
/// kurin-wide <c>Member:Update</c> the scope check holds to that kurin — and never by the person.
/// </summary>
public class MemberLevelConfirmationTests
{
    private static readonly Guid KurinKey = Guid.NewGuid();
    private static readonly DateTime Now = new(2026, 11, 17, 10, 0, 0, DateTimeKind.Utc);

    private readonly Mock<IMemberUnitOfWork> _unitOfWork = new();
    private readonly Mock<IMemberRepository> _members = new();
    private readonly Mock<IResourceAccessService> _access = new();
    private readonly Mock<ICurrentPermissions> _permissions = new();
    private readonly Mock<ICurrentUserContext> _user = new();
    private readonly JournalRecorder _journal;
    private readonly Guid _caller = Guid.NewGuid();
    private readonly Member _member;
    private readonly PlastLevelHistory _skob;
    private readonly PlastLevelHistory _waiting;

    public MemberLevelConfirmationTests()
    {
        _skob = new PlastLevelHistory { PlastLevelHistoryKey = Guid.NewGuid(), PlastLevel = PlastLevel.Skob, DateAchieved = new DateOnly(2021, 5, 1) };
        _waiting = new PlastLevelHistory { PlastLevelHistoryKey = Guid.NewGuid(), PlastLevel = PlastLevel.Starshoplastun, DateAchieved = new DateOnly(2025, 9, 1), AwaitsConfirmation = true };
        _member = new Member
        {
            MemberKey = Guid.NewGuid(),
            UserKey = Guid.NewGuid(),
            FirstName = "Олена",
            LastName = "Старша",
            Email = "o@example.com",
            PhoneNumber = "+380501234567",
            LatestPlastLevel = PlastLevel.Skob,
            PlastLevelHistory = [_skob, _waiting]
        };

        _unitOfWork.SetupGet(u => u.Members).Returns(_members.Object);
        _unitOfWork.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
        _members.Setup(m => m.GetByKeyAsync(_member.MemberKey, It.IsAny<CancellationToken>())).ReturnsAsync(_member);
        _user.SetupGet(u => u.UserId).Returns(_caller);
        _user.SetupGet(u => u.KurinKey).Returns(KurinKey);
        _journal = new JournalRecorder(_user.Object).Knows(_member.MemberKey, KurinKey, null, "Олена", "Старша", _member.UserKey);
        CallerHolds(AccessScope.KurinWide, allowedHere: true);
    }

    private void CallerHolds(AccessScope? scope, bool allowedHere)
    {
        _permissions.Setup(p => p.WidestScopeAsync(ResourceType.Member, ResourceAction.Update, It.IsAny<CancellationToken>())).ReturnsAsync(scope);
        _access.Setup(a => a.CheckAccessAsync(ResourceType.Member, ResourceAction.Update, _member.MemberKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(allowedHere ? ResourceAccessDecision.Allow() : ResourceAccessDecision.Deny("elsewhere"));
    }

    private PlastLevelConfirmationService Service() => new(
        _unitOfWork.Object, _access.Object, _permissions.Object, _user.Object, _journal.Audit, new FixedClock(Now));

    [Fact]
    public async Task Confirm_ByTheZvyazkovyi_MakesTheLevelCount_AndJournalsIt()
    {
        var result = await new ConfirmMemberLevelCommandHandler(Service())
            .Handle(new ConfirmMemberLevelCommand(_member.MemberKey, _waiting.PlastLevelHistoryKey), CancellationToken.None);

        result.Type.Should().Be(ResultType.Success);
        result.Data!.AwaitsConfirmation.Should().BeFalse();
        _waiting.AwaitsConfirmation.Should().BeFalse();
        _waiting.ConfirmedByUserKey.Should().Be(_caller);
        _waiting.ConfirmedAtUtc.Should().Be(Now);
        _member.LatestPlastLevel.Should().Be(PlastLevel.Starshoplastun);
        var entry = _journal.Entries.Single();
        entry.Action.Should().Be(AuditActionNames.Member.LevelConfirmed);
        entry.KurinKey.Should().Be(KurinKey);
        entry.Changes.Should().ContainSingle(change => change.Field == "plastLevels");
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Reject_ByTheZvyazkovyi_RemovesTheLevel_AndJournalsIt()
    {
        var result = await new RejectMemberLevelCommandHandler(Service())
            .Handle(new RejectMemberLevelCommand(_member.MemberKey, _waiting.PlastLevelHistoryKey), CancellationToken.None);

        result.Type.Should().Be(ResultType.Success);
        _member.PlastLevelHistory.Should().Equal(_skob);
        _member.LatestPlastLevel.Should().Be(PlastLevel.Skob);
        _journal.Entries.Single().Action.Should().Be(AuditActionNames.Member.LevelRejected);
    }

    [Theory]
    [InlineData(AccessScope.OwnGroups, true)]
    [InlineData(AccessScope.Own, true)]
    [InlineData(AccessScope.KurinWide, false)]
    public async Task Confirm_WithoutTheKurinWideRightWhereThePersonStands_IsRefused(AccessScope scope, bool allowedHere)
    {
        CallerHolds(scope, allowedHere);

        var result = await Service().ConfirmAsync(_member.MemberKey, _waiting.PlastLevelHistoryKey, CancellationToken.None);

        result.Type.Should().Be(ResultType.Forbidden);
        result.ErrorCode.Should().Be("LevelConfirmationNotAllowed");
        _waiting.AwaitsConfirmation.Should().BeTrue();
        _journal.Entries.Should().BeEmpty();
    }

    [Fact]
    public async Task Confirm_OwnLevel_IsRefused_EvenForTheZvyazkovyi()
    {
        _user.SetupGet(u => u.UserId).Returns(_member.UserKey);

        var result = await Service().RejectAsync(_member.MemberKey, _waiting.PlastLevelHistoryKey, CancellationToken.None);

        result.Type.Should().Be(ResultType.Forbidden);
        _member.PlastLevelHistory.Should().HaveCount(2);
    }

    [Fact]
    public async Task Confirm_ALevelThatAlreadyCounts_IsAConflict()
    {
        var result = await Service().ConfirmAsync(_member.MemberKey, _skob.PlastLevelHistoryKey, CancellationToken.None);

        result.Type.Should().Be(ResultType.Conflict);
        result.ErrorCode.Should().Be("LevelNotAwaitingConfirmation");
    }

    [Fact]
    public async Task Confirm_ALevelNotOnTheCard_IsNotFound()
    {
        var result = await Service().ConfirmAsync(_member.MemberKey, Guid.NewGuid(), CancellationToken.None);

        result.Type.Should().Be(ResultType.NotFound);
        result.ErrorCode.Should().Be("LevelNotFound");
    }

    private sealed class FixedClock(DateTime now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(now);
    }
}
