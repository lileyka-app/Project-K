using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Moq;
using ProjectK.BusinessLogic.Modules.KurinModule.Features.BranchRequest.Review;
using ProjectK.BusinessLogic.Modules.KurinModule.Features.BranchRequest.Submit;
using ProjectK.BusinessLogic.Services.Caching;
using ProjectK.Common.Entities.AuthModule;
using ProjectK.Common.Entities.KurinModule;
using ProjectK.Common.Interfaces;
using ProjectK.Common.Interfaces.Modules.InfrastructureModule;
using ProjectK.Common.Interfaces.Modules.KurinModule;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Events;
using ProjectK.Common.Models.Records;
using ProjectK.Common.Models.Settings;
using Xunit;

namespace ProjectK.BusinessLogic.Tests.KurinModule.HandlerTests.KurinHandlers;

public class KurinBranchRequestHandlerTests
{
    private readonly Guid _userKey = Guid.NewGuid();
    private readonly Mock<IKurinRepository> _kurins = new();
    private readonly Mock<IKurinBranchRequestRepository> _requests = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly Mock<ICurrentUserContext> _currentUser = new();
    private readonly Mock<IDomainEventPublisher> _events = new();
    private readonly Mock<IUnitOfWorkTransaction> _transaction = new();
    private readonly Mock<UserManager<AppUser>> _users;

    public KurinBranchRequestHandlerTests()
    {
        _unitOfWork.Setup(u => u.Kurins).Returns(_kurins.Object);
        _unitOfWork.Setup(u => u.KurinBranchRequests).Returns(_requests.Object);
        _unitOfWork.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
        _unitOfWork.Setup(u => u.BeginTransactionAsync(It.IsAny<CancellationToken>())).ReturnsAsync(_transaction.Object);
        _requests.Setup(r => r.TrySettleAsync(It.IsAny<Guid>(), It.IsAny<KurinBranchRequestStatus>(), It.IsAny<Guid?>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _currentUser.Setup(c => c.UserId).Returns(_userKey);
        _users = new Mock<UserManager<AppUser>>(Mock.Of<IUserStore<AppUser>>(), null!, null!, null!, null!, null!, null!, null!, null!);
    }

    private Kurin Given(Kurin kurin)
    {
        _kurins.Setup(r => r.GetByKeyAsync(kurin.KurinKey, It.IsAny<CancellationToken>())).ReturnsAsync(kurin);
        return kurin;
    }

    private SubmitKurinBranchRequestCommandHandler Submitter() => new(
        _unitOfWork.Object,
        _currentUser.Object,
        _users.Object,
        _events.Object,
        Options.Create(new KurinNumberOptions()));

    private ReviewKurinBranchRequestCommandHandler Reviewer() => new(
        _unitOfWork.Object,
        _currentUser.Object,
        Mock.Of<IKurinAudit>(),
        _events.Object,
        Mock.Of<IBackendCache>());

    [Fact]
    public void SetBranches_ShouldKeepTheMainBranchMirrored_AndOrderUspFirst()
    {
        var kurin = Kurin.InBranches([new KurinNumbering(KurinBranch.UPS, 20), new KurinNumbering(KurinBranch.USP, 5)]);

        kurin.Branch.Should().Be(KurinBranch.USP);
        kurin.Number.Should().Be(5);
        kurin.Numbering.Should().Equal(new KurinNumbering(KurinBranch.USP, 5), new KurinNumbering(KurinBranch.UPS, 20));

        kurin.SetBranches([new KurinNumbering(KurinBranch.UPS, 20)]);

        kurin.Branch.Should().Be(KurinBranch.UPS);
        kurin.Number.Should().Be(20);
        kurin.Branches.Should().ContainSingle();
    }

    [Fact]
    public void Score_ShouldBeOnInYouthAndOffInSeniorKurins_UntilTheKurinChooses()
    {
        var youth = new Kurin(47);
        var senior = new Kurin(5, KurinBranch.USP);

        youth.IsScoreEnabled.Should().BeTrue();
        senior.IsScoreEnabled.Should().BeFalse();

        youth.ScoreEnabled = false;
        senior.ScoreEnabled = true;

        youth.IsScoreEnabled.Should().BeFalse();
        senior.IsScoreEnabled.Should().BeTrue();
    }

    [Fact]
    public void BranchSet_ShouldNotMixYouthWithTheOlderBranches()
    {
        KurinBranchSet.IsValid([KurinBranch.UPYu]).Should().BeTrue();
        KurinBranchSet.IsValid([KurinBranch.USP, KurinBranch.UPS]).Should().BeTrue();
        KurinBranchSet.IsValid([KurinBranch.UPYu, KurinBranch.USP]).Should().BeFalse();
        KurinBranchSet.IsValid([KurinBranch.USP, KurinBranch.USP]).Should().BeFalse();
        KurinBranchSet.IsValid([]).Should().BeFalse();
    }

    [Fact]
    public async Task Submit_ShouldRefuse_AYouthKurin()
    {
        var kurin = Given(new Kurin(47));

        var result = await Submitter().Handle(new SubmitKurinBranchRequestCommand(kurin.KurinKey, 5, null, null), default);

        result.Type.Should().Be(ResultType.BadRequest);
        result.ErrorCode.Should().Be("NotSeniorKurin");
        _requests.Verify(r => r.Create(It.IsAny<KurinBranchRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Submit_ShouldRefuse_ANumberAnotherKurinHoldsInThatBranch()
    {
        var kurin = Given(new Kurin(5, KurinBranch.USP));
        _kurins.Setup(r => r.IsNumberTakenAsync(kurin.KraiCode, KurinBranch.UPS, 20, kurin.KurinKey, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var result = await Submitter().Handle(new SubmitKurinBranchRequestCommand(kurin.KurinKey, 5, 20, null), default);

        result.Type.Should().Be(ResultType.Conflict);
        result.ErrorCode.Should().Be("KurinNumberTaken");
    }

    [Fact]
    public async Task Submit_ShouldSaveTheRequest_AndTellTheAdministrators()
    {
        var kurin = Given(new Kurin(5, KurinBranch.USP) { NamedAfter = "Орден Хрестоносців" });

        var result = await Submitter().Handle(new SubmitKurinBranchRequestCommand(kurin.KurinKey, 5, 20, "  Ми вже рік у УПС  "), default);

        result.Type.Should().Be(ResultType.Created);
        result.Data!.WantedBranches.Should().Equal(new KurinNumbering(KurinBranch.USP, 5), new KurinNumbering(KurinBranch.UPS, 20));
        _requests.Verify(r => r.Create(
            It.Is<KurinBranchRequest>(request => request.UspNumber == 5 && request.UpsNumber == 20 && request.Note == "Ми вже рік у УПС" && request.RequestedByUserKey == _userKey),
            It.IsAny<CancellationToken>()), Times.Once);
        _events.Verify(e => e.PublishAsync(
            It.Is<KurinBranchRequestSubmitted>(raised => raised.Kurin == "«Орден Хрестоносців» · 5-й курінь УСП" && raised.Wanted == "5-й курінь УСП · 20-й курінь УПС"),
            It.IsAny<CancellationToken>()), Times.Once);
        kurin.Numbering.Should().ContainSingle("nothing changes before an administrator approves");
    }

    [Fact]
    public async Task Approve_ShouldGiveTheKurinTheBranchesItAskedFor()
    {
        var kurin = Given(new Kurin(5, KurinBranch.USP));
        var pending = new KurinBranchRequest { KurinKey = kurin.KurinKey, UspNumber = 5, UpsNumber = 20, RequestedByUserKey = Guid.NewGuid() };
        _requests.Setup(r => r.GetByKeyAsync(pending.KurinBranchRequestKey, It.IsAny<CancellationToken>())).ReturnsAsync(pending);

        var result = await Reviewer().Handle(new ReviewKurinBranchRequestCommand(pending.KurinBranchRequestKey, Approve: true), default);

        result.Type.Should().Be(ResultType.Success);
        kurin.Numbering.Should().Equal(new KurinNumbering(KurinBranch.USP, 5), new KurinNumbering(KurinBranch.UPS, 20));
        pending.Status.Should().Be(KurinBranchRequestStatus.Approved);
        _events.Verify(e => e.PublishAsync(
            It.Is<KurinBranchRequestReviewed>(raised => raised.Approved && raised.RequesterUserKey == pending.RequestedByUserKey),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Review_ShouldNotApplyAnything_WhenSomeoneSettledTheRequestFirst()
    {
        var kurin = Given(new Kurin(5, KurinBranch.USP));
        var pending = new KurinBranchRequest { KurinKey = kurin.KurinKey, UspNumber = 5, UpsNumber = 20 };
        _requests.Setup(r => r.GetByKeyAsync(pending.KurinBranchRequestKey, It.IsAny<CancellationToken>())).ReturnsAsync(pending);
        _requests.Setup(r => r.TrySettleAsync(pending.KurinBranchRequestKey, KurinBranchRequestStatus.Approved, It.IsAny<Guid?>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var result = await Reviewer().Handle(new ReviewKurinBranchRequestCommand(pending.KurinBranchRequestKey, Approve: true), default);

        result.Type.Should().Be(ResultType.Conflict);
        result.ErrorCode.Should().Be("KurinBranchRequestAlreadySettled");
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
        _transaction.Verify(t => t.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
        _events.Verify(e => e.PublishAsync(It.IsAny<KurinBranchRequestReviewed>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Submit_ShouldRefuseANoteLongerThanTheColumn()
    {
        var kurin = Given(new Kurin(5, KurinBranch.USP));

        var result = await Submitter().Handle(
            new SubmitKurinBranchRequestCommand(kurin.KurinKey, 5, 20, new string('я', KurinBranchRequest.NoteMaxLength + 1)), default);

        result.Type.Should().Be(ResultType.BadRequest);
        result.ErrorCode.Should().Be("NoteTooLong");
    }

    [Fact]
    public void KraiCode_ShouldTravelToEveryBranchRow()
    {
        var kurin = Kurin.InBranches([new KurinNumbering(KurinBranch.USP, 5), new KurinNumbering(KurinBranch.UPS, 20)]);

        kurin.KraiCode = "cz";
        kurin.SetBranches([new KurinNumbering(KurinBranch.USP, 5), new KurinNumbering(KurinBranch.UPS, 21)]);

        kurin.Branches.Should().OnlyContain(row => row.KraiCode == "cz");
    }

    [Fact]
    public async Task Reject_ShouldLeaveTheKurinAsItWas()
    {
        var kurin = Given(new Kurin(5, KurinBranch.USP));
        var pending = new KurinBranchRequest { KurinKey = kurin.KurinKey, UpsNumber = 20 };
        _requests.Setup(r => r.GetByKeyAsync(pending.KurinBranchRequestKey, It.IsAny<CancellationToken>())).ReturnsAsync(pending);

        var result = await Reviewer().Handle(new ReviewKurinBranchRequestCommand(pending.KurinBranchRequestKey, Approve: false), default);

        result.Type.Should().Be(ResultType.Success);
        kurin.Numbering.Should().Equal(new KurinNumbering(KurinBranch.USP, 5));
        pending.Status.Should().Be(KurinBranchRequestStatus.Rejected);
    }

    [Fact]
    public async Task Approve_ShouldRefuse_WhenTheNumberWasTakenMeanwhile()
    {
        var kurin = Given(new Kurin(5, KurinBranch.USP));
        var pending = new KurinBranchRequest { KurinKey = kurin.KurinKey, UspNumber = 5, UpsNumber = 20 };
        _requests.Setup(r => r.GetByKeyAsync(pending.KurinBranchRequestKey, It.IsAny<CancellationToken>())).ReturnsAsync(pending);
        _kurins.Setup(r => r.IsNumberTakenAsync(kurin.KraiCode, KurinBranch.UPS, 20, kurin.KurinKey, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var result = await Reviewer().Handle(new ReviewKurinBranchRequestCommand(pending.KurinBranchRequestKey, Approve: true), default);

        result.Type.Should().Be(ResultType.Conflict);
        pending.Status.Should().Be(KurinBranchRequestStatus.Pending);
        kurin.Numbering.Should().ContainSingle();
    }
}
