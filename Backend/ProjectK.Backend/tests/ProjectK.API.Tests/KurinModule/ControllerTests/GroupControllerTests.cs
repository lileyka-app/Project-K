using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using ProjectK.API.Controllers.KurinModule;
using ProjectK.API.Models.Requests;
using ProjectK.BusinessLogic.Modules.KurinModule.Features.Group.Delete;
using ProjectK.BusinessLogic.Modules.KurinModule.Features.Group.Get;
using ProjectK.BusinessLogic.Modules.KurinModule.Features.Group.Silhouette;
using ProjectK.BusinessLogic.Modules.KurinModule.Features.Group.Upsert;
using ProjectK.BusinessLogic.Modules.KurinModule.Features.MentorAssignment.Get;
using ProjectK.BusinessLogic.Modules.KurinModule.Models;
using ProjectK.Common.Models.Dtos.KurinModule;
using ProjectK.Common.Models.Dtos.KurinModule.Requests;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Records;

namespace ProjectK.API.Tests.KurinModule.ControllerTests;

public class GroupControllerTests
{
    private readonly Mock<IMediator> _mediatorMock;
    private readonly GroupController _controller;

    public GroupControllerTests()
    {
        _mediatorMock = new Mock<IMediator>();
        _controller = new GroupController(_mediatorMock.Object);
    }

    [Fact]
    public async Task GetByKey_ShouldReturnOk_WhenSuccess()
    {
        var key = Guid.NewGuid();
        var dto = new GroupResponse { GroupKey = key, Name = "Alpha", KurinKey = Guid.NewGuid(), KurinNumber = 5 };
        var serviceResult = new ServiceResult<GroupResponse>(ResultType.Success, dto);

        _mediatorMock
            .Setup(m => m.Send(It.IsAny<GetGroupByKeyQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(serviceResult);

        var result = await _controller.GetByKey(key);

        var ok = Assert.IsType<OkObjectResult>(result);
        var data = Assert.IsType<GroupResponse>(ok.Value);
        Assert.Equal(key, data.GroupKey);
    }

    [Fact]
    public async Task GetByKey_ShouldReturnNotFound_WhenNotFound()
    {
        var key = Guid.NewGuid();
        var serviceResult = new ServiceResult<GroupResponse>(ResultType.NotFound);

        _mediatorMock
            .Setup(m => m.Send(It.IsAny<GetGroupByKeyQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(serviceResult);

        var result = await _controller.GetByKey(key);

        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public async Task GetAll_ShouldReturnOk_WhenSuccess()
    {
        var kurinKey = Guid.NewGuid();
        var groups = new List<GroupResponse>
        {
            new() { GroupKey = Guid.NewGuid(), Name = "A", KurinKey = kurinKey, KurinNumber = 1 },
            new() { GroupKey = Guid.NewGuid(), Name = "B", KurinKey = kurinKey, KurinNumber = 1 }
        };
        var serviceResult = new ServiceResult<IEnumerable<GroupResponse>>(ResultType.Success, groups);

        _mediatorMock
            .Setup(m => m.Send(It.IsAny<GetGroupsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(serviceResult);

        var result = await _controller.GetAll(kurinKey);

        var ok = Assert.IsType<OkObjectResult>(result);
        var data = Assert.IsType<List<GroupResponse>>(ok.Value);
        Assert.Equal(groups.Count, data.Count);
    }

    [Fact]
    public async Task Create_ShouldReturnCreated_WhenSuccess()
    {
        var groupKey = Guid.NewGuid();
        var kurinKey = Guid.NewGuid();
        var response = new GroupResponse { GroupKey = groupKey, Name = "Alpha", KurinKey = kurinKey, KurinNumber = 10 };

        var serviceResult = new ServiceResult<GroupResponse>(
            ResultType.Created,
            response,
            "GetByKey",
            new { groupKey = groupKey }); // note: handler currently sets KurinKey by mistake

        _mediatorMock
            .Setup(m => m.Send(
                It.Is<UpsertGroupCommand>(command => command.Description == "Group description"),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(serviceResult);

        var createRequest = new CreateGroupRequest { Name = "Alpha", KurinKey = kurinKey, Description = "Group description" };

        var result = await _controller.Create(createRequest);

        var created = Assert.IsType<CreatedAtActionResult>(result);
        Assert.Equal("GetByKey", created.ActionName);
        var data = Assert.IsType<GroupResponse>(created.Value);
        Assert.Equal(groupKey, data.GroupKey);
    }

    [Fact]
    public async Task Create_ShouldReturnNotFound_WhenKurinMissing()
    {
        var kurinKey = Guid.NewGuid();
        var serviceResult = new ServiceResult<GroupResponse>(ResultType.NotFound);

        _mediatorMock
            .Setup(m => m.Send(It.IsAny<UpsertGroupCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(serviceResult);

        var createRequest = new CreateGroupRequest { Name = "Alpha", KurinKey = kurinKey };

        var result = await _controller.Create(createRequest);

        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public async Task Update_ShouldReturnOk_WhenSuccess()
    {
        var groupKey = Guid.NewGuid();
        var response = new GroupResponse { GroupKey = groupKey, Name = "Updated", KurinKey = Guid.NewGuid(), KurinNumber = 3 };
        var serviceResult = new ServiceResult<GroupResponse>(ResultType.Success, response);

        _mediatorMock
            .Setup(m => m.Send(
                It.Is<UpsertGroupCommand>(command => command.Description == "Updated description"),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(serviceResult);

        var updateRequest = new UpdateGroupRequest { Name = "Updated", Description = "Updated description" };

        var result = await _controller.Update(groupKey, updateRequest);

        var ok = Assert.IsType<OkObjectResult>(result);
        var data = Assert.IsType<GroupResponse>(ok.Value);
        Assert.Equal("Updated", data.Name);
    }

    [Fact]
    public async Task Update_ShouldReturnNotFound_WhenGroupNotFound()
    {
        var groupKey = Guid.NewGuid();
        var serviceResult = new ServiceResult<GroupResponse>(ResultType.NotFound);

        _mediatorMock
            .Setup(m => m.Send(It.IsAny<UpsertGroupCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(serviceResult);

        var updateRequest = new UpdateGroupRequest { Name = "X" };

        var result = await _controller.Update(groupKey, updateRequest);

        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public async Task UploadSilhouette_ShouldSendCommandAndReturnOk_WhenFileIsValid()
    {
        var groupKey = Guid.NewGuid();
        var response = new GroupResponse
        {
            GroupKey = groupKey,
            Name = "Alpha",
            KurinKey = Guid.NewGuid(),
            KurinNumber = 3,
            SilhouetteUrl = "group-silhouettes/2026/05/27/test.png"
        };
        var serviceResult = new ServiceResult<GroupResponse>(ResultType.Success, response);

        _mediatorMock
            .Setup(m => m.Send(
                It.Is<UploadGroupSilhouetteCommand>(command =>
                    command.GroupKey == groupKey &&
                    command.BlobFileName == "silhouette.png" &&
                    command.BlobContent.Length == 4),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(serviceResult);

        var file = CreateFormFile("silhouette.png", "image/png", [1, 2, 3, 4]);

        var result = await _controller.UploadSilhouette(groupKey, new UploadImageRequest { File = file }, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var data = Assert.IsType<GroupResponse>(ok.Value);
        Assert.Equal(response.SilhouetteUrl, data.SilhouetteUrl);
    }

    [Fact]
    public async Task UploadSilhouette_ShouldReturnBadRequest_WhenFileTypeIsUnsupported()
    {
        var groupKey = Guid.NewGuid();
        var file = CreateFormFile("silhouette.gif", "image/gif", [1, 2, 3, 4]);

        var result = await _controller.UploadSilhouette(groupKey, new UploadImageRequest { File = file }, CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
        _mediatorMock.Verify(m => m.Send(It.IsAny<UploadGroupSilhouetteCommand>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task DeleteSilhouette_ShouldSendCommandAndReturnOk()
    {
        var groupKey = Guid.NewGuid();
        var response = new GroupResponse { GroupKey = groupKey, Name = "Alpha", KurinKey = Guid.NewGuid(), KurinNumber = 3 };
        var serviceResult = new ServiceResult<GroupResponse>(ResultType.Success, response);

        _mediatorMock
            .Setup(m => m.Send(
                It.Is<DeleteGroupSilhouetteCommand>(command => command.GroupKey == groupKey),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(serviceResult);

        var result = await _controller.DeleteSilhouette(groupKey, CancellationToken.None);

        Assert.IsType<OkObjectResult>(result);
    }

    [Fact]
    public async Task Delete_ShouldReturnOk_WhenSuccess()
    {
        var groupKey = Guid.NewGuid();
        var serviceResult = new ServiceResult<object>(ResultType.Success);

        _mediatorMock
            .Setup(m => m.Send(It.IsAny<DeleteGroupCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(serviceResult);

        var result = await _controller.Delete(groupKey);

        // Assuming mapping like KurinController (Success -> 200 OK)
        Assert.IsType<OkObjectResult>(result);
    }

    [Fact]
    public async Task Delete_ShouldReturnNotFound_WhenGroupMissing()
    {
        var groupKey = Guid.NewGuid();
        var serviceResult = new ServiceResult<object>(ResultType.NotFound);

        _mediatorMock
            .Setup(m => m.Send(It.IsAny<DeleteGroupCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(serviceResult);

        var result = await _controller.Delete(groupKey);

        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public async Task Delete_ShouldReturnInternalServerError_WhenUnexpected()
    {
        var groupKey = Guid.NewGuid();
        var serviceResult = new ServiceResult<object>((ResultType)999);

        _mediatorMock
            .Setup(m => m.Send(It.IsAny<DeleteGroupCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(serviceResult);

        var result = await _controller.Delete(groupKey);

        var obj = Assert.IsType<ObjectResult>(result);
        Assert.Equal(500, obj.StatusCode);
    }

    [Fact]
    public async Task GetMentors_ShouldReturnOk_WhenSuccess()
    {
        var groupKey = Guid.NewGuid();
        var mentors = new List<MemberLookupDto>
        {
            new() { MemberKey = Guid.NewGuid(), UserKey = Guid.NewGuid(), FirstName = "I", LastName = "Mentor", MiddleName = "M" }
        };
        var serviceResult = new ServiceResult<IEnumerable<MemberLookupDto>>(ResultType.Success, mentors);

        _mediatorMock
            .Setup(m => m.Send(It.IsAny<GetGroupMentorsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(serviceResult);

        var result = await _controller.GetMentors(groupKey);

        var ok = Assert.IsType<OkObjectResult>(result);
        var data = Assert.IsType<List<MemberLookupDto>>(ok.Value);
        Assert.Single(data);
        Assert.Equal("Mentor", data[0].LastName);
    }

    private static IFormFile CreateFormFile(string fileName, string contentType, byte[] bytes)
    {
        var stream = new MemoryStream(bytes);
        return new FormFile(stream, 0, bytes.Length, "file", fileName)
        {
            Headers = new HeaderDictionary(),
            ContentType = contentType
        };
    }
}
