using FluentAssertions;
using Moq;
using ProjectK.BusinessLogic.Modules.KurinModule.Features.Membership.Join;
using ProjectK.BusinessLogic.Modules.KurinModule.Features.MembershipRequest.Cancel;
using ProjectK.BusinessLogic.Modules.KurinModule.Features.MembershipRequest.Create;
using ProjectK.BusinessLogic.Modules.KurinModule.Features.MembershipRequest.Decide;
using ProjectK.BusinessLogic.Modules.KurinModule.Features.MembershipRequest.Former;
using ProjectK.BusinessLogic.Modules.KurinModule.Features.MembershipRequest.Waiting;
using ProjectK.BusinessLogic.Modules.KurinModule.Services;
using ProjectK.BusinessLogic.Tests.TestHelpers;
using ProjectK.Common.Entities.KurinModule;
using ProjectK.Common.Exceptions;
using ProjectK.Common.Interfaces;
using ProjectK.Common.Interfaces.Modules.AuthModule;
using ProjectK.Common.Interfaces.Modules.DuesModule;
using ProjectK.Common.Interfaces.Modules.InfrastructureModule;
using ProjectK.Common.Interfaces.Modules.KurinModule;
using ProjectK.Common.Interfaces.Modules.MemberModule;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Events;
using ProjectK.Common.Models.Records;
using Xunit;

namespace ProjectK.BusinessLogic.Tests.KurinModule.HandlerTests.MembershipHandlers;

/// <summary>
/// Nobody moves between kurins on their own, and no kurin takes someone from another without asking:
/// kurin B asks, the Звʼязковий of kurin A answers, and debt does not leave with the person.
/// </summary>
public class MembershipRequestTests
{
    private const string Code = "PL-4F7A2-9D4A1";
    private static readonly DateTimeOffset Now = new(2026, 10, 30, 12, 0, 0, TimeSpan.Zero);

    private readonly Guid _kurinA = Guid.NewGuid();
    private readonly Guid _kurinB = Guid.NewGuid();
    private readonly Guid _kurinC = Guid.NewGuid();
    private readonly Guid _person = Guid.NewGuid();
    private readonly Guid _account = Guid.NewGuid();

    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly Mock<IMembershipRepository> _memberships = new();
    private readonly Mock<IMembershipRequestRepository> _requests = new();
    private readonly Mock<IMemberDirectory> _members = new();
    private readonly Mock<IDuesDirectory> _dues = new();
    private readonly Mock<IDomainEventPublisher> _events = new();
    private readonly Mock<ICurrentUserContext> _user = new();
    private readonly JournalRecorder _journal;
    private readonly FixedTimeProvider _time = new(Now);

    private readonly List<Membership> _standing = [];
    private readonly List<Membership> _opened = [];
    private readonly List<MembershipRequest> _created = [];
    private readonly List<MembershipRequest> _pending = [];
    private readonly List<MembershipRequest> _asked = [];

    public MembershipRequestTests()
    {
        _journal = new JournalRecorder(_user.Object);
        _unitOfWork.SetupGet(u => u.Memberships).Returns(_memberships.Object);
        _unitOfWork.SetupGet(u => u.MembershipRequests).Returns(_requests.Object);
        _unitOfWork.SetupGet(u => u.Groups).Returns(Mock.Of<IGroupRepository>());
        _unitOfWork.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        _memberships.Setup(r => r.GetActiveForMemberAsync(_person, It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => _standing.Where(m => m.LeftAtUtc is null).ToList());
        _memberships.Setup(r => r.GetActiveAsync(_person, It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid _, Guid kurin, CancellationToken _) => _standing.FirstOrDefault(m => m.KurinKey == kurin && m.LeftAtUtc is null));
        _memberships.Setup(r => r.GetRecordsForMemberAsync(_person, It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => _standing.Select(m => new MembershipRecord(m.MembershipKey, m.KurinKey, 1, KurinBranch.UPYu, null, m.GroupKey, null, m.Kind, m.JoinedAtUtc, m.LeftAtUtc)).ToList());
        _memberships.Setup(r => r.Open(It.IsAny<Membership>())).Callback<Membership>(m => { _opened.Add(m); _standing.Add(m); });

        _requests.Setup(r => r.Create(It.IsAny<MembershipRequest>(), It.IsAny<CancellationToken>()))
            .Callback<MembershipRequest, CancellationToken>((r, _) => _created.Add(r));
        _requests.Setup(r => r.GetPendingAsync(_person, _kurinB, It.IsAny<CancellationToken>())).ReturnsAsync(() => _pending);
        _requests.Setup(r => r.GetPendingFromAsync(_person, It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid _, Guid from, CancellationToken _) => _asked.Where(r => r.FromKurinKey == from && r.Status == MembershipRequestStatus.Pending).ToList());

        var summary = new MemberSummary(_person, _account, _kurinA, null, "Назар", "Коваль", "n@x", null);
        _members.Setup(d => d.FindAsync(_person, It.IsAny<CancellationToken>())).ReturnsAsync(summary);
        _members.Setup(d => d.FindByPublicIdAsync(Code, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MemberCard(_person, Code, "Назар", "Коваль", null, 1));
    }

    private void ActingIn(Guid kurinKey) => _user.SetupGet(u => u.KurinKey).Returns(kurinKey);

    private Membership StandsIn(Guid kurinKey)
    {
        var membership = new Membership { MemberKey = _person, UserKey = _account, KurinKey = kurinKey, JoinedAtUtc = Now.UtcDateTime.AddYears(-2) };
        _standing.Add(membership);
        return membership;
    }

    private MembershipMoves Moves() => new(_unitOfWork.Object, _journal.Audit, _events.Object, _dues.Object, _time);

    private CreateMembershipRequestCommandHandler Create() =>
        new(_unitOfWork.Object, _members.Object, Moves(), _events.Object, _user.Object, Mock.Of<IAccessChangeSignal>(), _time);

    private DecideMembershipRequestCommandHandler Decide() =>
        new(_unitOfWork.Object, _members.Object, Moves(), _events.Object, _user.Object, Mock.Of<IAccessChangeSignal>(), _time);

    private MembershipRequest Asked(MembershipRequestKind kind, DateTime? expires = null, Guid? by = null)
    {
        var request = new MembershipRequest
        {
            Kind = kind,
            MemberKey = _person,
            FromKurinKey = _kurinA,
            ToKurinKey = by ?? _kurinB,
            CreatedAtUtc = Now.UtcDateTime.AddDays(-1),
            ExpiresAtUtc = expires ?? Now.UtcDateTime.AddDays(13)
        };
        _requests.Setup(r => r.GetAsync(request.MembershipRequestKey, It.IsAny<CancellationToken>())).ReturnsAsync(request);
        _asked.Add(request);
        return request;
    }

    private void Owes(decimal amount) =>
        _dues.Setup(d => d.GetOwedAsync(_kurinA, It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>())).ReturnsAsync(amount);

    // ── Asking ──────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task AskingForSomeoneInAnotherKurin_SendsTheRequestThere_AndOpensNothing()
    {
        ActingIn(_kurinB);
        StandsIn(_kurinA);

        var result = await Create().Handle(new CreateMembershipRequestCommand(_kurinB, Code, null, MembershipRequestKind.Transfer, MembershipKind.Youth), default);

        result.Type.Should().Be(ResultType.Created);
        result.Data!.Joined.Should().BeFalse();
        var request = _created.Should().ContainSingle().Subject;
        request.FromKurinKey.Should().Be(_kurinA);
        request.ToKurinKey.Should().Be(_kurinB);
        request.ExpiresAtUtc.Should().Be(Now.UtcDateTime.AddDays(14));
        _opened.Should().BeEmpty();
        _events.Verify(e => e.PublishAsync(It.Is<MembershipRequested>(r => r.FromKurinKey == _kurinA), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SomeoneWhoStandsNowhere_IsTakenInAtOnce()
    {
        ActingIn(_kurinB);

        var result = await Create().Handle(new CreateMembershipRequestCommand(_kurinB, Code, null, MembershipRequestKind.Transfer, MembershipKind.Youth), default);

        result.Data!.Joined.Should().BeTrue();
        _opened.Should().ContainSingle(m => m.KurinKey == _kurinB && m.UserKey == _account);
        _created.Should().BeEmpty();
    }

    [Fact]
    public async Task SomeoneInSeveralKurins_NeedsTheKurinToAsk()
    {
        ActingIn(_kurinB);
        StandsIn(_kurinA);
        StandsIn(Guid.NewGuid());

        var result = await Create().Handle(new CreateMembershipRequestCommand(_kurinB, Code, null, MembershipRequestKind.AlsoJoin, MembershipKind.Staff), default);

        result.ErrorCode.Should().Be("ChooseKurin");
        _created.Should().BeEmpty();
    }

    [Fact]
    public async Task AskingTwice_IsRefusedWhileTheFirstWaits()
    {
        ActingIn(_kurinB);
        StandsIn(_kurinA);
        _pending.Add(Asked(MembershipRequestKind.Transfer));

        var result = await Create().Handle(new CreateMembershipRequestCommand(_kurinB, Code, null, MembershipRequestKind.Transfer, MembershipKind.Youth), default);

        result.ErrorCode.Should().Be("RequestPending");
    }

    [Fact]
    public async Task AKeyOfSomeoneWhoNeverStoodHere_IsUnknown()
    {
        ActingIn(_kurinB);
        StandsIn(_kurinA);

        var result = await Create().Handle(new CreateMembershipRequestCommand(_kurinB, null, _person, MembershipRequestKind.Transfer, MembershipKind.Youth), default);

        result.Type.Should().Be(ResultType.NotFound);
    }

    [Fact]
    public async Task AskingFromAnotherKurinsScope_IsForbidden()
    {
        ActingIn(_kurinA);

        var result = await Create().Handle(new CreateMembershipRequestCommand(_kurinB, Code, null, MembershipRequestKind.Transfer, MembershipKind.Youth), default);

        result.Type.Should().Be(ResultType.Forbidden);
    }

    // ── Answering ───────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task ApprovingATransfer_ClosesKurinA_AndOpensKurinB()
    {
        ActingIn(_kurinA);
        var inA = StandsIn(_kurinA);
        Owes(0);
        var request = Asked(MembershipRequestKind.Transfer);

        var result = await Decide().Handle(new DecideMembershipRequestCommand(_kurinA, request.MembershipRequestKey, Approve: true), default);

        result.Type.Should().Be(ResultType.Success);
        inA.LeftAtUtc.Should().NotBeNull();
        _opened.Should().ContainSingle(m => m.KurinKey == _kurinB);
        request.Status.Should().Be(MembershipRequestStatus.Approved);
        _events.Verify(e => e.PublishAsync(It.Is<MembershipLeft>(l => l.KurinKey == _kurinA), It.IsAny<CancellationToken>()), Times.Once);
        _events.Verify(e => e.PublishAsync(It.Is<MembershipRequestDecided>(d => d.Approved), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ATransfer_WaitsWhileThePersonOwesKurinA()
    {
        ActingIn(_kurinA);
        var inA = StandsIn(_kurinA);
        Owes(460);
        var request = Asked(MembershipRequestKind.Transfer);

        var result = await Decide().Handle(new DecideMembershipRequestCommand(_kurinA, request.MembershipRequestKey, Approve: true), default);

        result.ErrorCode.Should().Be("MemberOwesDues");
        inA.LeftAtUtc.Should().BeNull();
        _opened.Should().BeEmpty();
        request.Status.Should().Be(MembershipRequestStatus.Pending);
    }

    [Fact]
    public async Task AlsoJoin_KeepsKurinA_AndIgnoresDebt()
    {
        ActingIn(_kurinA);
        var inA = StandsIn(_kurinA);
        Owes(460);
        var request = Asked(MembershipRequestKind.AlsoJoin);

        await Decide().Handle(new DecideMembershipRequestCommand(_kurinA, request.MembershipRequestKey, Approve: true), default);

        inA.LeftAtUtc.Should().BeNull();
        _opened.Should().ContainSingle(m => m.KurinKey == _kurinB);
    }

    [Fact]
    public async Task Declining_ChangesNoMembership()
    {
        ActingIn(_kurinA);
        var inA = StandsIn(_kurinA);
        var request = Asked(MembershipRequestKind.Transfer);

        await Decide().Handle(new DecideMembershipRequestCommand(_kurinA, request.MembershipRequestKey, Approve: false), default);

        request.Status.Should().Be(MembershipRequestStatus.Declined);
        inA.LeftAtUtc.Should().BeNull();
        _opened.Should().BeEmpty();
    }

    [Fact]
    public async Task ALapsedRequest_CannotBeApproved()
    {
        ActingIn(_kurinA);
        StandsIn(_kurinA);
        var request = Asked(MembershipRequestKind.Transfer, expires: Now.UtcDateTime.AddMinutes(-1));

        var result = await Decide().Handle(new DecideMembershipRequestCommand(_kurinA, request.MembershipRequestKey, Approve: true), default);

        result.ErrorCode.Should().Be("RequestClosed");
        _opened.Should().BeEmpty();
    }

    [Fact]
    public async Task TwoKurinsAskingForOnePerson_OnlyTheFirstApprovedGetsThem()
    {
        ActingIn(_kurinA);
        StandsIn(_kurinA);
        Owes(0);
        var toB = Asked(MembershipRequestKind.Transfer);
        var toC = Asked(MembershipRequestKind.Transfer, by: _kurinC);

        await Decide().Handle(new DecideMembershipRequestCommand(_kurinA, toB.MembershipRequestKey, Approve: true), default);

        toC.IsOpen(Now.UtcDateTime).Should().BeFalse();
        toC.Status.Should().Be(MembershipRequestStatus.Pending);
        toB.Status.Should().Be(MembershipRequestStatus.Approved);
        toB.ExpiresAtUtc.Should().Be(Now.UtcDateTime.AddDays(13));

        var second = await Decide().Handle(new DecideMembershipRequestCommand(_kurinA, toC.MembershipRequestKey, Approve: true), default);

        second.ErrorCode.Should().Be("RequestClosed");
        _opened.Should().ContainSingle().Which.KurinKey.Should().Be(_kurinB);
    }

    [Fact]
    public async Task ARequestAboutSomeoneWhoHasLeft_CannotBeApproved_AndLapses()
    {
        ActingIn(_kurinA);
        StandsIn(_kurinA).LeftAtUtc = Now.UtcDateTime.AddDays(-1);
        var request = Asked(MembershipRequestKind.AlsoJoin);

        var result = await Decide().Handle(new DecideMembershipRequestCommand(_kurinA, request.MembershipRequestKey, Approve: true), default);

        result.ErrorCode.Should().Be("MemberNotHere");
        request.IsOpen(Now.UtcDateTime).Should().BeFalse();
        _opened.Should().BeEmpty();
    }

    [Fact]
    public async Task TwoAnswersAtOnce_TheSecondSaveIsRefused()
    {
        ActingIn(_kurinA);
        StandsIn(_kurinA);
        var request = Asked(MembershipRequestKind.Transfer);
        _unitOfWork.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new StaleRowException(new Exception()));

        var result = await Decide().Handle(new DecideMembershipRequestCommand(_kurinA, request.MembershipRequestKey, Approve: false), default);

        result.Type.Should().Be(ResultType.Conflict);
        result.ErrorCode.Should().Be("RequestClosed");
    }

    [Fact]
    public async Task TheMenusDot_CountsWaitingRequests_WithoutReadingDebt()
    {
        ActingIn(_kurinA);
        _requests.Setup(r => r.CountOpenToAnswerAsync(_kurinA, Now.UtcDateTime, It.IsAny<CancellationToken>())).ReturnsAsync(2);

        var result = await new CountWaitingMembershipRequestsQueryHandler(_unitOfWork.Object, _user.Object, _time)
            .Handle(new CountWaitingMembershipRequestsQuery(_kurinA), default);

        result.Data.Should().Be(2);
        _dues.Verify(d => d.GetOwedAsync(It.IsAny<Guid>(), It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task TheMenusDot_OfAnotherKurin_IsForbidden()
    {
        ActingIn(_kurinB);

        var result = await new CountWaitingMembershipRequestsQueryHandler(_unitOfWork.Object, _user.Object, _time)
            .Handle(new CountWaitingMembershipRequestsQuery(_kurinA), default);

        result.Type.Should().Be(ResultType.Forbidden);
    }

    [Fact]
    public async Task OnlyTheAskedKurin_Answers()
    {
        ActingIn(_kurinB);
        var request = Asked(MembershipRequestKind.Transfer);

        var result = await Decide().Handle(new DecideMembershipRequestCommand(_kurinB, request.MembershipRequestKey, Approve: true), default);

        result.Type.Should().Be(ResultType.NotFound);
    }

    // ── Withdrawing ─────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task TheAskingKurin_WithdrawsItsRequest_AndOnlyIt()
    {
        var request = Asked(MembershipRequestKind.Transfer);
        var cancel = new CancelMembershipRequestCommandHandler(_unitOfWork.Object, _user.Object, _time);

        ActingIn(_kurinA);
        (await cancel.Handle(new CancelMembershipRequestCommand(_kurinA, request.MembershipRequestKey), default)).Type.Should().Be(ResultType.NotFound);

        ActingIn(_kurinB);
        (await cancel.Handle(new CancelMembershipRequestCommand(_kurinB, request.MembershipRequestKey), default)).Type.Should().Be(ResultType.Success);
        request.Status.Should().Be(MembershipRequestStatus.Cancelled);
    }

    // ── Taking straight ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task TakingByHand_SomeoneWhoStandsElsewhere_SendsYouToARequest()
    {
        StandsIn(_kurinA);
        _unitOfWork.SetupGet(u => u.Kurins).Returns(Mock.Of<IKurinRepository>(r =>
            r.GetByKeyAsync(_kurinB, It.IsAny<CancellationToken>()) == Task.FromResult<Kurin?>(new Kurin(15))));
        var join = new JoinKurinCommandHandler(_unitOfWork.Object, _members.Object, Mock.Of<IAccessChangeSignal>(), _journal.Audit);

        var result = await join.Handle(
            new JoinKurinCommand(Guid.Empty, _kurinB, null, MembershipKind.Youth, Code, OnlyIfStandingNowhere: true), default);

        result.ErrorCode.Should().Be("StandsInAnotherKurin");
        _opened.Should().BeEmpty();
    }

    // ── Former members ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task FormerMembers_ComeWithTheKurinsTheyStandInNow()
    {
        ActingIn(_kurinB);
        StandsIn(_kurinA);
        var nowhere = Guid.NewGuid();
        _memberships.Setup(r => r.GetFormerInKurinAsync(_kurinB, It.IsAny<CancellationToken>())).ReturnsAsync(
        [
            new FormerMember(_person, "Назар", null, "Коваль", null, null, Now.UtcDateTime.AddYears(-3), Now.UtcDateTime.AddYears(-1)),
            new FormerMember(nowhere, "Віталій", null, "Бойко", null, null, Now.UtcDateTime.AddYears(-3), Now.UtcDateTime.AddMonths(-2))
        ]);
        _memberships.Setup(r => r.GetActiveForMemberAsync(nowhere, It.IsAny<CancellationToken>())).ReturnsAsync([]);
        _unitOfWork.SetupGet(u => u.Kurins).Returns(Mock.Of<IKurinRepository>(r =>
            r.GetByKeyAsync(_kurinA, It.IsAny<CancellationToken>()) == Task.FromResult<Kurin?>(new Kurin(1))));

        var result = await new GetFormerStandingQueryHandler(_unitOfWork.Object, _user.Object).Handle(new GetFormerStandingQuery(_kurinB), default);

        result.Data!.Should().HaveCount(2);
        result.Data![0].StandsIn.Should().ContainSingle().Which.Number.Should().Be(1);
        result.Data![1].StandsIn.Should().BeEmpty();
    }

    [Fact]
    public async Task FormerMembers_OfAnotherKurin_AreForbidden()
    {
        ActingIn(_kurinA);

        var result = await new GetFormerStandingQueryHandler(_unitOfWork.Object, _user.Object).Handle(new GetFormerStandingQuery(_kurinB), default);

        result.Type.Should().Be(ResultType.Forbidden);
    }
}
