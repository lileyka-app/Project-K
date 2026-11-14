using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using Moq;
using ProjectK.BusinessLogic.Modules.KurinModule.Features.Group;
using ProjectK.BusinessLogic.Modules.KurinModule.Models;
using ProjectK.BusinessLogic.Services.Caching;
using ProjectK.BusinessLogic.Tests.TestHelpers;
using ProjectK.Common.Contracts.InfrastructureModule;
using ProjectK.Common.Contracts.KurinModule;
using ProjectK.Common.Contracts.Shared;
using ProjectK.Common.Entities.KurinModule;
using ProjectK.Common.Models.Dtos.KurinModule;
using ProjectK.Common.Models.Enums;

namespace ProjectK.BusinessLogic.Tests.KurinModule.HandlerTests.GroupHandlers;

public class GetGroupByKeyHandlerTests
{
    private readonly IBlobReadLinks _links;
    private readonly Mock<IGroupRepository> _groupRepositoryMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly GetGroupByKeyQueryHandler _handler;

    public GetGroupByKeyHandlerTests()
    {
        _links = FakeBlobReadLinks.Instance;

        _groupRepositoryMock = new Mock<IGroupRepository>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();

        _unitOfWorkMock.Setup(u => u.Groups).Returns(_groupRepositoryMock.Object);

        _handler = new GetGroupByKeyQueryHandler(_unitOfWorkMock.Object, _links, CreateCache());
    }

    private static IBackendCache CreateCache() =>
        new MemoryBackendCache(new MemoryCache(new MemoryCacheOptions()), Microsoft.Extensions.Logging.Abstractions.NullLogger<MemoryBackendCache>.Instance);

    [Fact]
    public async Task Handle_WhenGroupExists_ShouldReturnSuccessWithMappedResponse()
    {
        // Arrange
        var kurin = new Kurin(17) { KurinKey = Guid.NewGuid() };
        var groupKey = Guid.NewGuid();
        var group = new Group("Alpha", kurin.KurinKey)
        {
            GroupKey = groupKey,
            Kurin = kurin,
            Description = "Group description"
        };
        var query = new GetGroupByKeyQuery(groupKey);

        _groupRepositoryMock
            .Setup(r => r.GetByKeyAsync(groupKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(group);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Type.Should().Be(ResultType.Success);
        result.Data.Should().NotBeNull();
        result.Data.GroupKey.Should().Be(groupKey);
        result.Data.KurinKey.Should().Be(kurin.KurinKey);
        result.Data.Name.Should().Be("Alpha");
        result.Data.Description.Should().Be("Group description");
        result.Data.KurinNumber.Should().Be(kurin.Number);

        _groupRepositoryMock.Verify(r => r.GetByKeyAsync(groupKey, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenGroupDoesNotExist_ShouldReturnNotFound()
    {
        // Arrange
        var groupKey = Guid.NewGuid();
        var query = new GetGroupByKeyQuery(groupKey);

        _groupRepositoryMock
            .Setup(r => r.GetByKeyAsync(groupKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Group)null!);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Type.Should().Be(ResultType.NotFound);
        result.Data.Should().BeNull();

        _groupRepositoryMock.Verify(r => r.GetByKeyAsync(groupKey, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenRepositoryThrowsException_ShouldPropagateException()
    {
        // Arrange
        var groupKey = Guid.NewGuid();
        var query = new GetGroupByKeyQuery(groupKey);
        var expected = new Exception("DB failure");

        _groupRepositoryMock
            .Setup(r => r.GetByKeyAsync(groupKey, It.IsAny<CancellationToken>()))
            .ThrowsAsync(expected);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<Exception>(() =>
            _handler.Handle(query, CancellationToken.None));

        ex.Should().BeSameAs(expected);
        _groupRepositoryMock.Verify(r => r.GetByKeyAsync(groupKey, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenMappingWorks_ShouldMapCorrectly()
    {
        // Arrange
        var kurin = new Kurin(99) { KurinKey = Guid.NewGuid() };
        var group = new Group("Bravo", kurin.KurinKey)
        {
            GroupKey = Guid.NewGuid(),
            Kurin = kurin
        };
        var query = new GetGroupByKeyQuery(group.GroupKey);

        _groupRepositoryMock
            .Setup(r => r.GetByKeyAsync(group.GroupKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(group);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Type.Should().Be(ResultType.Success);
        result.Data.Should().BeEquivalentTo(new { group.GroupKey, group.KurinKey, group.Name, group.Description, SilhouetteUrl = FakeBlobReadLinks.Instance.For(group.SilhouetteBlobName), KurinNumber = group.Kurin?.Number });
    }

    [Fact]
    public async Task Handle_WhenCalledTwiceWithSameGroupKey_ShouldUseCache()
    {
        var kurin = new Kurin(99) { KurinKey = Guid.NewGuid() };
        var group = new Group("Bravo", kurin.KurinKey)
        {
            GroupKey = Guid.NewGuid(),
            Kurin = kurin
        };

        _groupRepositoryMock
            .Setup(r => r.GetByKeyAsync(group.GroupKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(group);

        await _handler.Handle(new GetGroupByKeyQuery(group.GroupKey), CancellationToken.None);
        await _handler.Handle(new GetGroupByKeyQuery(group.GroupKey), CancellationToken.None);

        _groupRepositoryMock.Verify(r => r.GetByKeyAsync(group.GroupKey, It.IsAny<CancellationToken>()), Times.Once);
    }
}
