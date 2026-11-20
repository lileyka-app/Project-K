using FluentAssertions;
using Moq;
using ProjectK.BusinessLogic.Modules.KurinModule.Features.Group;
using ProjectK.BusinessLogic.Services.Caching;
using ProjectK.BusinessLogic.Tests.TestHelpers;
using ProjectK.Common.Contracts.KurinModule;
using ProjectK.Common.Contracts.Shared;
using ProjectK.Common.Entities.KurinModule;
using ProjectK.Common.Enums.Shared;
using ProjectK.Common.Records.Shared;

namespace ProjectK.BusinessLogic.Tests.KurinModule.HandlerTests.GroupHandlers;

public sealed class GroupSilhouetteHandlerTests
{
    private readonly IBlobReadLinks _links;
    private readonly Mock<IGroupRepository> _groupRepositoryMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly Mock<IPhotoService> _photoServiceMock;
    private readonly Mock<IBackendCache> _cacheMock;

    public GroupSilhouetteHandlerTests()
    {
        _links = FakeBlobReadLinks.Instance;

        _groupRepositoryMock = new Mock<IGroupRepository>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _photoServiceMock = new Mock<IPhotoService>();
        _cacheMock = new Mock<IBackendCache>();

        _unitOfWorkMock.Setup(u => u.Groups).Returns(_groupRepositoryMock.Object);
    }

    [Fact]
    public async Task Upload_ShouldStoreNewSilhouetteAndDeleteOldBlob()
    {
        var kurin = new Kurin(10) { KurinKey = Guid.NewGuid() };
        var groupKey = Guid.NewGuid();
        var group = new Group("Alpha", kurin.KurinKey) { GroupKey = groupKey, Kurin = kurin, SilhouetteBlobName = "old.png" };
        var handler = new UploadGroupSilhouetteCommandHandler(_unitOfWorkMock.Object, _photoServiceMock.Object, _links, _cacheMock.Object);

        _groupRepositoryMock
            .Setup(r => r.GetByKeyAsync(groupKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(group);
        _photoServiceMock
            .Setup(p => p.UploadPhotoAsync(
                It.IsAny<byte[]>(),
                "silhouette.jpg",
                BlobUploadContext.GroupSilhouette,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PhotoUploadResult("group-silhouettes/2026/05/27/new.png", "https://cdn/new.png"));
        _unitOfWorkMock
            .Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var result = await handler.Handle(new UploadGroupSilhouetteCommand(groupKey, [1, 2, 3], "silhouette.jpg"), CancellationToken.None);

        result.Type.Should().Be(ResultType.Success);
        group.SilhouetteBlobName.Should().Be("group-silhouettes/2026/05/27/new.png");
        result.Data!.SilhouetteUrl.Should().Be("https://cdn.test/group-silhouettes/2026/05/27/new.png?sig=read");
        _groupRepositoryMock.Verify(r => r.Update(group, It.IsAny<CancellationToken>()), Times.Once);
        _photoServiceMock.Verify(p => p.DeletePhotoAsync("old.png", It.IsAny<CancellationToken>()), Times.Once);
        _cacheMock.Verify(c => c.Invalidate(BackendCacheRules.GroupReads), Times.Once);
    }

    [Fact]
    public async Task Upload_ShouldReturnBadRequest_WhenImageIsInvalid()
    {
        var kurin = new Kurin(10) { KurinKey = Guid.NewGuid() };
        var groupKey = Guid.NewGuid();
        var group = new Group("Alpha", kurin.KurinKey) { GroupKey = groupKey, Kurin = kurin };
        var handler = new UploadGroupSilhouetteCommandHandler(_unitOfWorkMock.Object, _photoServiceMock.Object, _links, _cacheMock.Object);

        _groupRepositoryMock
            .Setup(r => r.GetByKeyAsync(groupKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(group);
        _photoServiceMock
            .Setup(p => p.UploadPhotoAsync(
                It.IsAny<byte[]>(),
                It.IsAny<string>(),
                BlobUploadContext.GroupSilhouette,
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("bad image"));

        var result = await handler.Handle(new UploadGroupSilhouetteCommand(groupKey, [1], "bad.png"), CancellationToken.None);

        result.Type.Should().Be(ResultType.BadRequest);
        result.ErrorCode.Should().Be("InvalidImageContent");
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Delete_ShouldClearSilhouetteAndDeleteBlob()
    {
        var kurin = new Kurin(10) { KurinKey = Guid.NewGuid() };
        var groupKey = Guid.NewGuid();
        var group = new Group("Alpha", kurin.KurinKey) { GroupKey = groupKey, Kurin = kurin, SilhouetteBlobName = "old.png" };
        var handler = new DeleteGroupSilhouetteCommandHandler(_unitOfWorkMock.Object, _photoServiceMock.Object, _links, _cacheMock.Object);

        _groupRepositoryMock
            .Setup(r => r.GetByKeyAsync(groupKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(group);
        _unitOfWorkMock
            .Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var result = await handler.Handle(new DeleteGroupSilhouetteCommand(groupKey), CancellationToken.None);

        result.Type.Should().Be(ResultType.Success);
        group.SilhouetteBlobName.Should().BeNull();
        result.Data!.SilhouetteUrl.Should().BeNull();
        _photoServiceMock.Verify(p => p.DeletePhotoAsync("old.png", It.IsAny<CancellationToken>()), Times.Once);
        _cacheMock.Verify(c => c.Invalidate(BackendCacheRules.GroupReads), Times.Once);
    }
}
