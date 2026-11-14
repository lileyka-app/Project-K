using MediatR;
using Microsoft.AspNetCore.Mvc;
using Moq;
using ProjectK.API.Controllers.KurinModule;
using ProjectK.BusinessLogic.Modules.KurinModule.Features.Leadership;
using ProjectK.Common.Dtos.KurinModule;
using ProjectK.Common.Dtos.KurinModule.Requests;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Records;

namespace ProjectK.API.Tests.KurinModule.ControllerTests;

public class LeadershipControllerTests
{
    private readonly Mock<IMediator> _mediatorMock;
    private readonly LeadershipController _controller;

    public LeadershipControllerTests()
    {
        _mediatorMock = new Mock<IMediator>(MockBehavior.Strict);
        _controller = new LeadershipController(_mediatorMock.Object);
    }

    private static LeadershipDto SampleLeadershipResponse(Guid? key = null) => new(
        LeadershipKey: key ?? Guid.NewGuid(),
        Type: LeadershipType.Kurin,
        EntityKey: Guid.NewGuid(),
        KurinKey: Guid.NewGuid(),
        GroupKey: null,
        LeadershipHistories: new List<LeadershipHistoryMemberDto>(),
        StartDate: new DateOnly(2024, 1, 1),
        EndDate: null);

    private static UpsertLeadershipRequest SampleUpsertRequest() => new()
    {
        Type = "kurin",
        EntityKey = Guid.NewGuid(),
        StartDate = new DateOnly(2024, 1, 1),
        EndDate = null,
        LeadershipHistories = new List<LeadershipHistoryMemberDto>()
    };

    // GetLeadershipByTypeQuery

    [Fact]
    public async Task GetLeadershipByType_ShouldReturnOk_WhenSuccess()
    {
        var typeString = "kurin";
        var typeKey = Guid.NewGuid();
        var dto = SampleLeadershipResponse();
        var result = new ServiceResult<LeadershipDto>(ResultType.Success, dto);

        _mediatorMock
            .Setup(m => m.Send(It.Is<GetLeadershipByTypeQuery>(q => q.LeadershipType == LeadershipType.Kurin && q.TypeKey == typeKey), It.IsAny<CancellationToken>()))
            .ReturnsAsync(result);

        var token = new CancellationTokenSource().Token;
        var actionResult = await _controller.GetLeadershipByType(typeString, typeKey, token);

        var ok = Assert.IsType<OkObjectResult>(actionResult);
        var data = Assert.IsType<LeadershipDto>(ok.Value);
        Assert.Equal(dto.LeadershipKey, data.LeadershipKey);

        _mediatorMock.Verify(m => m.Send(It.IsAny<GetLeadershipByTypeQuery>(), token), Times.Once);
    }

    [Fact]
    public async Task GetLeadershipByType_ShouldReturnNotFound_WhenNotFound()
    {
        var result = new ServiceResult<LeadershipDto>(ResultType.NotFound);

        _mediatorMock
            .Setup(m => m.Send(It.IsAny<GetLeadershipByTypeQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(result);

        var actionResult = await _controller.GetLeadershipByType("group", Guid.NewGuid(), CancellationToken.None);

        Assert.IsType<NotFoundObjectResult>(actionResult);
    }

    [Fact]
    public async Task GetLeadershipByType_ShouldReturnInternalServerError_WhenUnhandledResultType()
    {
        var result = new ServiceResult<LeadershipDto>(ResultType.InternalServerError);

        _mediatorMock
            .Setup(m => m.Send(It.IsAny<GetLeadershipByTypeQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(result);

        var actionResult = await _controller.GetLeadershipByType("kv", Guid.NewGuid(), CancellationToken.None);

        var obj = Assert.IsType<ObjectResult>(actionResult);
        Assert.Equal(500, obj.StatusCode);
    }

    [Fact]
    public async Task GetLeadershipByType_ShouldThrow_WhenInvalidTypeString()
    {
        // Invalid enum value -> Enum.Parse inside  constructor throws
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _controller.GetLeadershipByType("invalid-type", Guid.NewGuid(), CancellationToken.None));

        _mediatorMock.Verify(m => m.Send(It.IsAny<GetLeadershipByTypeQuery>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // GetLeadershipByKeyQuery

    [Fact]
    public async Task GetLeadershipByKey_ShouldReturnOk_WhenSuccess()
    {
        var key = Guid.NewGuid();
        var dto = SampleLeadershipResponse(key);
        var result = new ServiceResult<LeadershipDto>(ResultType.Success, dto);

        _mediatorMock
            .Setup(m => m.Send(It.Is<GetLeadershipByKeyQuery>(q => q.LeadershipKey == key), It.IsAny<CancellationToken>()))
            .ReturnsAsync(result);

        var actionResult = await _controller.GetLeadershipByKey(key);

        var ok = Assert.IsType<OkObjectResult>(actionResult);
        var data = Assert.IsType<LeadershipDto>(ok.Value);
        Assert.Equal(key, data.LeadershipKey);
    }

    [Fact]
    public async Task GetLeadershipByKey_ShouldReturnNotFound_WhenNotFound()
    {
        _mediatorMock
            .Setup(m => m.Send(It.IsAny<GetLeadershipByKeyQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ServiceResult<LeadershipDto>(ResultType.NotFound));

        var actionResult = await _controller.GetLeadershipByKey(Guid.NewGuid());

        Assert.IsType<NotFoundObjectResult>(actionResult);
    }

    // CreateLeadership

    [Fact]
    public async Task CreateLeadership_ShouldReturnCreatedAtAction_WhenCreatedWithActionName()
    {
        var request = SampleUpsertRequest();
        var dto = SampleLeadershipResponse();
        var result = new ServiceResult<LeadershipDto>(
            ResultType.Created,
            dto,
            "GetLeadershipByKey",
            new { leadershipKey = dto.LeadershipKey });

        _mediatorMock
            .Setup(m => m.Send(It.Is<UpsertLeadershipCommand>(c =>
                    c.LeadershipKey == null &&
                    c.Type == request.Type &&
                    c.EntityKey == request.EntityKey),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(result);

        var actionResult = await _controller.CreateLeadership(request);

        var created = Assert.IsType<CreatedAtActionResult>(actionResult);
        Assert.Equal("GetLeadershipByKey", created.ActionName);
        Assert.Equal(dto, created.Value);
    }

    [Fact]
    public async Task CreateLeadership_ShouldReturnCreated_WhenCreatedWithoutActionName()
    {
        var request = SampleUpsertRequest();
        var dto = SampleLeadershipResponse();
        var result = new ServiceResult<LeadershipDto>(ResultType.Created, dto);

        _mediatorMock
            .Setup(m => m.Send(It.IsAny<UpsertLeadershipCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(result);

        var actionResult = await _controller.CreateLeadership(request);

        var created = Assert.IsType<CreatedResult>(actionResult);
        Assert.Equal(dto, created.Value);
    }

    [Fact]
    public async Task CreateLeadership_ShouldReturnBadRequest_WhenBadRequest()
    {
        var request = SampleUpsertRequest();
        var result = new ServiceResult<LeadershipDto>(ResultType.BadRequest, null);

        _mediatorMock
            .Setup(m => m.Send(It.IsAny<UpsertLeadershipCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(result);

        var actionResult = await _controller.CreateLeadership(request);

        Assert.IsType<BadRequestObjectResult>(actionResult);
    }

    [Fact]
    public async Task CreateLeadership_ShouldReturnConflict_WhenConflict()
    {
        var request = SampleUpsertRequest();
        var dto = SampleLeadershipResponse();
        var result = new ServiceResult<LeadershipDto>(ResultType.Conflict, dto);

        _mediatorMock
            .Setup(m => m.Send(It.IsAny<UpsertLeadershipCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(result);

        var actionResult = await _controller.CreateLeadership(request);

        var conflict = Assert.IsType<ConflictObjectResult>(actionResult);
        var payload = Assert.IsType<object[]>(conflict.Value);
        Assert.Equal("The entity that was attempted to be created already exists.", payload[0]);
        Assert.Equal(dto, payload[1]);
    }

    // UpdateLeadership

    [Fact]
    public async Task UpdateLeadership_ShouldReturnOk_WhenSuccess()
    {
        var key = Guid.NewGuid();
        var request = SampleUpsertRequest();
        var dto = SampleLeadershipResponse(key);
        var result = new ServiceResult<LeadershipDto>(ResultType.Success, dto);

        _mediatorMock
            .Setup(m => m.Send(It.Is<UpsertLeadershipCommand>(c =>
                    c.LeadershipKey == key &&
                    c.Type == request.Type),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(result);

        var actionResult = await _controller.UpdateLeadership(key, request);

        var ok = Assert.IsType<OkObjectResult>(actionResult);
        var data = Assert.IsType<LeadershipDto>(ok.Value);
        Assert.Equal(key, data.LeadershipKey);
    }

    [Fact]
    public async Task UpdateLeadership_ShouldReturnNotFound_WhenNotFound()
    {
        _mediatorMock
            .Setup(m => m.Send(It.IsAny<UpsertLeadershipCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ServiceResult<LeadershipDto>(ResultType.NotFound));

        var actionResult = await _controller.UpdateLeadership(Guid.NewGuid(), SampleUpsertRequest());

        Assert.IsType<NotFoundObjectResult>(actionResult);
    }

    [Fact]
    public async Task UpdateLeadership_ShouldReturnInternalServerError_WhenUnhandledResultType()
    {
        _mediatorMock
            .Setup(m => m.Send(It.IsAny<UpsertLeadershipCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ServiceResult<LeadershipDto>(ResultType.InternalServerError));

        var actionResult = await _controller.UpdateLeadership(Guid.NewGuid(), SampleUpsertRequest());

        var obj = Assert.IsType<ObjectResult>(actionResult);
        Assert.Equal(500, obj.StatusCode);
    }

    // GetLeadershipHistoriesQuery

    [Fact]
    public async Task GetLeadershipHistories_ShouldReturnOk_WhenSuccess()
    {
        var key = Guid.NewGuid();
        var histories = new List<LeadershipHistoryMemberDto>
        {
            new() { LeadershipHistoryKey = Guid.NewGuid(), LeadershipKey = key, Role = "Head", StartDate = new DateOnly(2024,1,1), EndDate = null, Member = new MemberLookupDto { MemberKey = Guid.NewGuid(), FirstName = "Test", LastName = "User" } }
        };
        var result = new ServiceResult<IEnumerable<LeadershipHistoryMemberDto>>(ResultType.Success, histories);

        _mediatorMock
            .Setup(m => m.Send(It.Is<GetLeadershipHistoriesQuery>(q => q.LeadershipKey == key), It.IsAny<CancellationToken>()))
            .ReturnsAsync(result);

        var actionResult = await _controller.GetLeadershipHistories(key);

        var ok = Assert.IsType<OkObjectResult>(actionResult);
        var data = Assert.IsAssignableFrom<IEnumerable<LeadershipHistoryMemberDto>>(ok.Value);
        Assert.Single(data);
    }

    [Fact]
    public async Task GetLeadershipHistories_ShouldReturnNotFound_WhenNotFound()
    {
        _mediatorMock
            .Setup(m => m.Send(It.IsAny<GetLeadershipHistoriesQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ServiceResult<IEnumerable<LeadershipHistoryMemberDto>>(ResultType.NotFound));

        var actionResult = await _controller.GetLeadershipHistories(Guid.NewGuid());

        Assert.IsType<NotFoundObjectResult>(actionResult);
    }
}
