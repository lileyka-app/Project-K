using MediatR;
using Microsoft.AspNetCore.Mvc;
using Moq;
using ProjectK.API.Controllers.KurinModule;
using ProjectK.BusinessLogic.Modules.KurinModule.Features.Kurin.Delete;
using ProjectK.BusinessLogic.Modules.KurinModule.Features.Kurin.Get;
using ProjectK.BusinessLogic.Modules.KurinModule.Features.Kurin.Upsert;
using ProjectK.BusinessLogic.Modules.KurinModule.Models;
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
        var serviceResult = new ServiceResult<KurinResponse>(ResultType.Success, new KurinResponse { KurinKey = key, Number = 1 });

        _mediatorMock
            .Setup(m => m.Send(It.IsAny<GetKurinByKeyQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(serviceResult);

        var result = await _controller.GetByKey(key);

        var ok = Assert.IsType<OkObjectResult>(result);
        var data = Assert.IsType<KurinResponse>(ok.Value);
        Assert.Equal(key, data.KurinKey);
    }

    [Fact]
    public async Task GetByKey_ShouldReturnNotFound_WhenNotFound()
    {
        var key = Guid.NewGuid();
        var serviceResult = new ServiceResult<KurinResponse>(ResultType.NotFound);

        _mediatorMock
            .Setup(m => m.Send(It.IsAny<GetKurinByKeyQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(serviceResult);

        var result = await _controller.GetByKey(key);

        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public async Task GetAll_ShouldReturnOk_WhenSuccess()
    {
        var kurins = new List<KurinResponse>
        {
            new KurinResponse { KurinKey = Guid.NewGuid(), Number = 1 },
            new KurinResponse { KurinKey = Guid.NewGuid(), Number = 2 }
        };
        var serviceResult = new ServiceResult<IEnumerable<KurinResponse>>(ResultType.Success, kurins);
        _mediatorMock
            .Setup(m => m.Send(It.IsAny<GetKurinsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(serviceResult);
        var result = await _controller.GetAll();
        var ok = Assert.IsType<OkObjectResult>(result);
        var data = Assert.IsType<List<KurinResponse>>(ok.Value);
        Assert.Equal(kurins.Count, data.Count);
    }

    [Fact]
    public async Task Create_ShouldReturnCreated_WhenSuccess()
    {
        var key = Guid.NewGuid();
        var kurinNumber = 5;
        var serviceResult = new ServiceResult<KurinResponse>(
            ResultType.Created,
            new KurinResponse { KurinKey = key, Number = kurinNumber },
            "GetByKey",
            new { kurinKey = key });

        _mediatorMock
            .Setup(m => m.Send(It.IsAny<UpsertKurinCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(serviceResult);

        var result = await _controller.Create(new KurinController.CreateKurinRequest { Number = kurinNumber });

        var created = Assert.IsType<CreatedAtActionResult>(result);
        var data = Assert.IsType<KurinResponse>(created.Value);
        Assert.Equal(kurinNumber, data.Number);
    }

    [Fact]
    public async Task Upsert_ShouldReturnBadRequest_WhenInvalid()
    {
        var key = Guid.NewGuid();
        var serviceResult = new ServiceResult<KurinResponse>(ResultType.BadRequest, new KurinResponse());

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
        var serviceResult = new ServiceResult<KurinResponse>(
            ResultType.Success,
            new KurinResponse { KurinKey = key, Number = 10, ProfileVerificationEnabled = true });

        _mediatorMock
            .Setup(m => m.Send(It.IsAny<UpsertKurinCommand>(), It.IsAny<CancellationToken>()))
            .Callback<IRequest<ServiceResult<KurinResponse>>, CancellationToken>((command, _) => sentCommand = (UpsertKurinCommand)command)
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
        var serviceResult = new ServiceResult<object>(ResultType.Success);

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
        var serviceResult = new ServiceResult<object>((ResultType)999); // невідомий тип

        _mediatorMock
            .Setup(m => m.Send(It.IsAny<DeleteKurinCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(serviceResult);

        var result = await _controller.Delete(key);

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(500, objectResult.StatusCode);
    }
}
