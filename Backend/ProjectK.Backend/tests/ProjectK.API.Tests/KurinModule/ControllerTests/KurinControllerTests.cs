using MediatR;
using Microsoft.AspNetCore.Mvc;
using Moq;
using ProjectK.API.Controllers.KurinModule;
using ProjectK.BusinessLogic.Modules.KurinModule.Features.Kurin;
using ProjectK.Common.Models.Dtos.KurinModule;
using ProjectK.Common.Models.Dtos.KurinModule.Requests;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Records;

namespace ProjectK.BusinessLogic.Tests.KurinModule.ControllerTests;

public class KurinControllerTests
{
    private readonly Mock<IMediator> _mediatorMock;
    private readonly KurinController _controller;

    public KurinControllerTests()
    {
        _mediatorMock = new Mock<IMediator>();
        _controller = new KurinController(_mediatorMock.Object);
    }

    [Fact]
    public async Task GetByKey_ShouldReturnOk_WhenSuccess()
    {
        var key = Guid.NewGuid();
        var serviceResult = new ServiceResult<KurinDto>(ResultType.Success, Kurin(key, 1));

        _mediatorMock
            .Setup(m => m.Send(It.IsAny<GetKurinByKeyQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(serviceResult);

        var result = await _controller.GetByKey(key);

        var ok = Assert.IsType<OkObjectResult>(result);
        var data = Assert.IsType<KurinDto>(ok.Value);
        Assert.Equal(key, data.KurinKey);
    }

    [Fact]
    public async Task GetByKey_ShouldReturnNotFound_WhenNotFound()
    {
        var key = Guid.NewGuid();
        var serviceResult = new ServiceResult<KurinDto>(ResultType.NotFound);

        _mediatorMock
            .Setup(m => m.Send(It.IsAny<GetKurinByKeyQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(serviceResult);

        var result = await _controller.GetByKey(key);

        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public async Task GetAll_ShouldReturnOk_WhenSuccess()
    {
        var kurins = new List<KurinDto>
        {
            Kurin(Guid.NewGuid(), 1),
            Kurin(Guid.NewGuid(), 2)
        };
        var serviceResult = new ServiceResult<IEnumerable<KurinDto>>(ResultType.Success, kurins);
        _mediatorMock
            .Setup(m => m.Send(It.IsAny<GetKurinsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(serviceResult);
        var result = await _controller.GetAll();
        var ok = Assert.IsType<OkObjectResult>(result);
        var data = Assert.IsType<List<KurinDto>>(ok.Value);
        Assert.Equal(kurins.Count, data.Count);
    }

    [Fact]
    public async Task Create_ShouldReturnCreated_WhenSuccess()
    {
        var key = Guid.NewGuid();
        var kurinNumber = 5;
        var serviceResult = new ServiceResult<KurinDto>(
            ResultType.Created,
            Kurin(key, kurinNumber),
            "GetByKey",
            new { kurinKey = key });

        _mediatorMock
            .Setup(m => m.Send(It.IsAny<UpsertKurinCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(serviceResult);

        var result = await _controller.Create(new CreateKurinRequest { Number = kurinNumber });

        var created = Assert.IsType<CreatedAtActionResult>(result);
        var data = Assert.IsType<KurinDto>(created.Value);
        Assert.Equal(kurinNumber, data.Number);
    }

    [Fact]
    public async Task Upsert_ShouldReturnBadRequest_WhenInvalid()
    {
        var key = Guid.NewGuid();
        var serviceResult = new ServiceResult<KurinDto>(ResultType.BadRequest, Kurin(Guid.Empty, null));

        _mediatorMock
            .Setup(m => m.Send(It.IsAny<UpsertKurinCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(serviceResult);

        var result = await _controller.Upsert(key, new UpdateKurinRequest { Number = 10 });

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task Upsert_ShouldSendProfileVerificationEnabled()
    {
        var key = Guid.NewGuid();
        UpsertKurinCommand? sentCommand = null;
        var serviceResult = new ServiceResult<KurinDto>(
            ResultType.Success,
            Kurin(key, 10, profileVerificationEnabled: true));

        _mediatorMock
            .Setup(m => m.Send(It.IsAny<UpsertKurinCommand>(), It.IsAny<CancellationToken>()))
            .Callback<IRequest<ServiceResult<KurinDto>>, CancellationToken>((command, _) => sentCommand = (UpsertKurinCommand)command)
            .ReturnsAsync(serviceResult);

        var result = await _controller.Upsert(key, new UpdateKurinRequest
        {
            Number = 10,
            ProfileVerificationEnabled = true
        });

        Assert.IsType<OkObjectResult>(result);
        Assert.NotNull(sentCommand);
        Assert.True(sentCommand.ProfileVerificationEnabled);
    }

    [Fact]
    public async Task Delete_ShouldReturnOk_WhenSuccess()
    {
        var key = Guid.NewGuid();
        var serviceResult = new ServiceResult<Unit>(ResultType.Success, Unit.Value);

        _mediatorMock
            .Setup(m => m.Send(It.IsAny<DeleteKurinCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(serviceResult);

        var result = await _controller.Delete(key);

        Assert.IsType<OkObjectResult>(result);
    }

    [Fact]
    public async Task Delete_ShouldReturnInternalServerError_WhenUnexpected()
    {
        var key = Guid.NewGuid();
        var serviceResult = new ServiceResult<Unit>((ResultType)999); // невідомий тип

        _mediatorMock
            .Setup(m => m.Send(It.IsAny<DeleteKurinCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(serviceResult);

        var result = await _controller.Delete(key);

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(500, objectResult.StatusCode);
    }

    private static KurinDto Kurin(Guid key, int? number, bool profileVerificationEnabled = false) => new(
        key, number, KurinBranch.UPYu, [], "ua", null, null, null, null, 0, profileVerificationEnabled, true,
        new Dictionary<string, string>(), null, false);
}
