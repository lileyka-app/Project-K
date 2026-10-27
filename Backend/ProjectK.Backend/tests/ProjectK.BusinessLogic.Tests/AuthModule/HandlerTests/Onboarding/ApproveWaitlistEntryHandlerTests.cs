using FluentAssertions;
using Microsoft.Extensions.Options;
using Moq;
using ProjectK.BusinessLogic.Modules.AuthModule.Features.Onboarding.ApproveWaitlistEntry;
using ProjectK.BusinessLogic.Services.Caching;
using ProjectK.Common.Entities.AuthModule;
using ProjectK.Common.Entities.KraiModule;
using ProjectK.Common.Entities.KurinModule;
using ProjectK.Common.Interfaces;
using ProjectK.Common.Interfaces.Modules.AuthModule;
using ProjectK.Common.Interfaces.Modules.InfrastructureModule;
using ProjectK.Common.Interfaces.Modules.KraiModule;
using ProjectK.Common.Interfaces.Modules.KurinModule;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Records;
using ProjectK.Common.Models.Settings;

namespace ProjectK.BusinessLogic.Tests.AuthModule.HandlerTests.Onboarding;

public class ApproveWaitlistEntryHandlerTests
{
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly Mock<IWaitlistRepository> _waitlist = new();
    private readonly Mock<IKurinRepository> _kurins = new();
    private readonly Mock<IKraiRepository> _krais = new();
    private readonly Mock<IAccountProvisioningService> _provisioning = new();
    private readonly Mock<IBackendCache> _cache = new();
    private readonly ApproveWaitlistEntryCommandHandler _handler;
    private Kurin? _openedKurin;

    public ApproveWaitlistEntryHandlerTests()
    {
        _unitOfWork.Setup(x => x.WaitlistEntries).Returns(_waitlist.Object);
        _unitOfWork.Setup(x => x.Kurins).Returns(_kurins.Object);
        _unitOfWork.Setup(x => x.Krais).Returns(_krais.Object);
        _unitOfWork.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
        _kurins
            .Setup(x => x.Create(It.IsAny<Kurin>(), It.IsAny<CancellationToken>()))
            .Callback<Kurin, CancellationToken>((kurin, _) => _openedKurin = kurin);
        _provisioning
            .Setup(x => x.ProvisionAsync(It.IsAny<AccountProvisioningRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ServiceResult<AccountProvisioningResult>(
                ResultType.Success, new AccountProvisioningResult(Guid.NewGuid(), Guid.NewGuid(), "token")));

        _handler = new ApproveWaitlistEntryCommandHandler(
            _unitOfWork.Object,
            _provisioning.Object,
            new Mock<IEmailService>().Object,
            new Mock<ICurrentUserContext>().Object,
            Options.Create(new KurinNumberOptions()),
            Options.Create(new KraiOptions()),
            _cache.Object);
    }

    [Fact]
    public async Task Handle_ShouldOpenTheClaimedNumber_WhenItIsFree()
    {
        var entry = Given(claimed: "47");

        var result = await _handler.Handle(new ApproveWaitlistEntryCommand(entry.WaitlistEntryKey), CancellationToken.None);

        result.Type.Should().Be(ResultType.Success);
        _openedKurin!.Number.Should().Be(47);
        entry.FoundedKurinKey.Should().Be(_openedKurin.KurinKey);
        // The admin's cached list of kurins must show the one just opened.
        _cache.Verify(x => x.Invalidate(BackendCachePolicies.KurinReads), Times.Once);
    }

    [Fact]
    public async Task Handle_ShouldOpenInTheDefaultKrai_WhenNobodyPickedOne()
    {
        var entry = Given(claimed: "47");

        await _handler.Handle(new ApproveWaitlistEntryCommand(entry.WaitlistEntryKey), CancellationToken.None);

        _openedKurin!.KraiCode.Should().Be(Krai.Ukraine);
        entry.KraiCode.Should().Be(Krai.Ukraine);
    }

    [Fact]
    public async Task Handle_ShouldOpenInTheApplicantsKrai_AndCheckTheNumberThere()
    {
        var entry = Given(claimed: "47", krai: Krai.Poland);
        // Ukraine's kurin 47 does not stand in the way of Poland's.
        _kurins.Setup(x => x.ExistsAsync(Krai.Ukraine, 47, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var result = await _handler.Handle(new ApproveWaitlistEntryCommand(entry.WaitlistEntryKey), CancellationToken.None);

        result.Type.Should().Be(ResultType.Success);
        _openedKurin!.KraiCode.Should().Be(Krai.Poland);
        _kurins.Verify(x => x.ExistsAsync(Krai.Poland, 47, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ShouldOpenInTheAdminsKrai_WhenTheyChangeIt()
    {
        var entry = Given(claimed: "47", krai: Krai.Ukraine);
        _krais.Setup(x => x.ExistsAsync(Krai.Poland, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var result = await _handler.Handle(new ApproveWaitlistEntryCommand(entry.WaitlistEntryKey, " PL "), CancellationToken.None);

        result.Type.Should().Be(ResultType.Success);
        _openedKurin!.KraiCode.Should().Be(Krai.Poland);
        entry.KraiCode.Should().Be(Krai.Poland);
    }

    [Fact]
    public async Task Handle_ShouldRefuse_WhenTheAdminsKraiIsUnknown()
    {
        var entry = Given(claimed: "47");

        var result = await _handler.Handle(new ApproveWaitlistEntryCommand(entry.WaitlistEntryKey, "zz"), CancellationToken.None);

        result.Type.Should().Be(ResultType.BadRequest);
        result.ErrorCode.Should().Be("KraiNotFound");
        _provisioning.Verify(x => x.ProvisionAsync(It.IsAny<AccountProvisioningRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ShouldOpenAPreparatoryKurin_WhenTheApplicantLeadsOne()
    {
        var entry = Given(claimed: null, preparatory: true);

        await _handler.Handle(new ApproveWaitlistEntryCommand(entry.WaitlistEntryKey), CancellationToken.None);

        _openedKurin!.Number.Should().BeNull();
    }

    // The first outside applicant wrote "0" before the form knew about підготовчі курені.
    [Theory]
    [InlineData("0")]
    [InlineData("1153")]
    public async Task Handle_ShouldOpenAPreparatoryKurin_WhenTheClaimIsOutsideTheRange(string claimed)
    {
        var entry = Given(claimed);

        await _handler.Handle(new ApproveWaitlistEntryCommand(entry.WaitlistEntryKey), CancellationToken.None);

        _openedKurin!.Number.Should().BeNull();
    }

    [Fact]
    public async Task Handle_ShouldRefuse_WhenTheClaimedNumberIsTaken()
    {
        var entry = Given(claimed: "47");
        _kurins.Setup(x => x.ExistsAsync("ua", 47, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var result = await _handler.Handle(new ApproveWaitlistEntryCommand(entry.WaitlistEntryKey), CancellationToken.None);

        result.Type.Should().Be(ResultType.Conflict);
        result.ErrorCode.Should().Be("KurinNumberTaken");
        _provisioning.Verify(x => x.ProvisionAsync(It.IsAny<AccountProvisioningRequest>(), It.IsAny<CancellationToken>()), Times.Never);
        _unitOfWork.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    private WaitlistEntry Given(string? claimed, bool preparatory = false, string? krai = null)
    {
        var entry = new WaitlistEntry
        {
            WaitlistEntryKey = Guid.NewGuid(),
            FirstName = "Olena",
            LastName = "Lviv",
            Email = "olena@example.com",
            IsKurinLeaderCandidate = true,
            ClaimedKurinNameOrNumber = claimed,
            IsPreparatoryKurin = preparatory,
            KraiCode = krai,
            VerificationStatus = WaitlistVerificationStatus.Submitted
        };
        _waitlist.Setup(x => x.GetByKeyAsync(entry.WaitlistEntryKey, It.IsAny<CancellationToken>())).ReturnsAsync(entry);
        return entry;
    }
}
