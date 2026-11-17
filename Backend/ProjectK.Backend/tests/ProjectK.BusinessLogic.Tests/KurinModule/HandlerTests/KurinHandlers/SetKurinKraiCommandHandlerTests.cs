using FluentAssertions;
using Moq;
using ProjectK.BusinessLogic.Modules.KurinModule.Features.Kurin;
using ProjectK.BusinessLogic.Services.Caching;
using ProjectK.BusinessLogic.Tests.TestHelpers;
using ProjectK.Common.Contracts.KraiModule;
using ProjectK.Common.Contracts.KurinModule;
using ProjectK.Common.Contracts.MemberModule;
using ProjectK.Common.Contracts.Shared;
using ProjectK.Common.Entities.KurinModule;
using ProjectK.Common.Enums.KurinModule;
using ProjectK.Common.Enums.Shared;

namespace ProjectK.BusinessLogic.Tests.KurinModule.HandlerTests.KurinHandlers;

public class SetKurinKraiCommandHandlerTests
{
    private readonly Mock<IKurinRepository> _kurins = new();
    private readonly Mock<IKraiRepository> _krais = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly SetKurinKraiCommandHandler _handler;

    public SetKurinKraiCommandHandlerTests()
    {
        _krais.Setup(r => r.ExistsAsync(It.IsIn("ua", "cz"), It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _unitOfWork.Setup(u => u.Kurins).Returns(_kurins.Object);
        _unitOfWork.Setup(u => u.Krais).Returns(_krais.Object);
        _unitOfWork.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        _handler = new SetKurinKraiCommandHandler(_unitOfWork.Object, FakeBlobReadLinks.Instance, new Mock<IBackendCache>().Object, Mock.Of<IMembershipDirectory>());
    }

    private Kurin Given(int? number, string krai = "ua")
    {
        var kurin = new Kurin(number) { KraiCode = krai };
        _kurins.Setup(r => r.GetByKeyAsync(kurin.KurinKey, It.IsAny<CancellationToken>())).ReturnsAsync(kurin);
        return kurin;
    }

    [Fact]
    public async Task Handle_ShouldMoveTheKurin_WhenItsNumberIsFreeThere()
    {
        var kurin = Given(12);

        var result = await _handler.Handle(new SetKurinKraiCommand(kurin.KurinKey, " CZ "), default);

        result.Type.Should().Be(ResultType.Success);
        kurin.KraiCode.Should().Be("cz");
        result.Data!.KraiCode.Should().Be("cz");
    }

    [Fact]
    public async Task Handle_ShouldRefuse_WhenTheNumberIsTakenInTheTargetKrai()
    {
        var kurin = Given(12);
        _kurins.Setup(r => r.IsNumberTakenAsync("cz", KurinBranch.UPYu, 12, kurin.KurinKey, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var result = await _handler.Handle(new SetKurinKraiCommand(kurin.KurinKey, "cz"), default);

        result.Type.Should().Be(ResultType.Conflict);
        result.ErrorCode.Should().Be("KurinNumberTaken");
        kurin.KraiCode.Should().Be("ua");
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ShouldMoveAPreparatoryKurin_WithoutANumberCheck()
    {
        var kurin = Given(null);

        var result = await _handler.Handle(new SetKurinKraiCommand(kurin.KurinKey, "cz"), default);

        result.Type.Should().Be(ResultType.Success);
        _kurins.Verify(r => r.IsNumberTakenAsync(It.IsAny<string>(), It.IsAny<KurinBranch>(), It.IsAny<int>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ShouldRefuse_AnUnknownKrai()
    {
        var kurin = Given(12);

        var result = await _handler.Handle(new SetKurinKraiCommand(kurin.KurinKey, "xx"), default);

        result.Type.Should().Be(ResultType.BadRequest);
        result.ErrorCode.Should().Be("UnknownKrai");
    }

    [Fact]
    public async Task Handle_ShouldReturnNotFound_ForAMissingKurin()
    {
        var result = await _handler.Handle(new SetKurinKraiCommand(Guid.NewGuid(), "cz"), default);

        result.Type.Should().Be(ResultType.NotFound);
    }
}
