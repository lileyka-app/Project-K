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

public class GetGroupsHandlerTests
{
    private readonly IBlobReadLinks _links;
    private readonly Mock<IGroupRepository> _groupRepositoryMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly GetGroupsQueryHandler _handler;

    public GetGroupsHandlerTests()
    {
        _links = FakeBlobReadLinks.Instance;

        _groupRepositoryMock = new Mock<IGroupRepository>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();

        _unitOfWorkMock.Setup(u => u.Groups).Returns(_groupRepositoryMock.Object);

        _handler = new GetGroupsQueryHandler(_unitOfWorkMock.Object, _links, CreateCache());
    }

    private static IBackendCache CreateCache() =>
        new MemoryBackendCache(new MemoryCache(new MemoryCacheOptions()), Microsoft.Extensions.Logging.Abstractions.NullLogger<MemoryBackendCache>.Instance);

    [Fact]
    public async Task Handle_WhenGroupsExist_ShouldReturnSuccessWithMappedResponses()
    {
        // Arrange
        var kurinKey = Guid.NewGuid();
        var kurin = new Kurin(7) { KurinKey = kurinKey };

        var groups = new List<Group>
        {
            new Group("Alpha", kurinKey) { GroupKey = Guid.NewGuid(), Kurin = kurin, Description = "A desc" },
            new Group("Bravo", kurinKey) { GroupKey = Guid.NewGuid(), Kurin = kurin, Description = "B desc" },
            new Group("Charlie", kurinKey) { GroupKey = Guid.NewGuid(), Kurin = kurin, Description = "C desc" }
        };

        _groupRepositoryMock
            .Setup(r => r.GetAllAsync(kurinKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(groups);

        var query = new GetGroupsQuery(kurinKey);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Type.Should().Be(ResultType.Success);
        result.Data.Should().NotBeNull();
        result.Data.Should().HaveCount(3);

        foreach (var g in groups)
        {
            result.Data.Should().Contain(gr =>
                gr.GroupKey == g.GroupKey &&
                gr.KurinKey == g.KurinKey &&
                gr.Name == g.Name &&
                gr.Description == g.Description &&
                gr.KurinNumber == kurin.Number);
        }

        _groupRepositoryMock.Verify(r => r.GetAllAsync(kurinKey, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenNoGroupsExist_ShouldReturnSuccessWithEmptyCollection()
    {
        // Arrange
        var kurinKey = Guid.NewGuid();
        _groupRepositoryMock
            .Setup(r => r.GetAllAsync(kurinKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Group>());

        var query = new GetGroupsQuery(kurinKey);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Type.Should().Be(ResultType.Success);
        result.Data.Should().NotBeNull();
        result.Data.Should().BeEmpty();

        _groupRepositoryMock.Verify(r => r.GetAllAsync(kurinKey, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenRepositoryThrowsException_ShouldPropagateException()
    {
        // Arrange
        var kurinKey = Guid.NewGuid();
        var expected = new Exception("Database error");

        _groupRepositoryMock
            .Setup(r => r.GetAllAsync(kurinKey, It.IsAny<CancellationToken>()))
            .ThrowsAsync(expected);

        var query = new GetGroupsQuery(kurinKey);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<Exception>(() =>
            _handler.Handle(query, CancellationToken.None));

        ex.Should().BeSameAs(expected);
        _groupRepositoryMock.Verify(r => r.GetAllAsync(kurinKey, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenMappingWorks_ShouldMapCorrectly()
    {
        // Arrange
        var kurinKey = Guid.NewGuid();
        var kurin = new Kurin(42) { KurinKey = kurinKey };
        var groups = new List<Group>
        {
            new Group("Delta", kurinKey) { GroupKey = Guid.NewGuid(), Kurin = kurin },
            new Group("Echo", kurinKey) { GroupKey = Guid.NewGuid(), Kurin = kurin }
        };

        _groupRepositoryMock
            .Setup(r => r.GetAllAsync(kurinKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(groups);

        var query = new GetGroupsQuery(kurinKey);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Type.Should().Be(ResultType.Success);
        result.Data!.Select(group => (group.GroupKey, group.Name, group.KurinKey)).Should().Equal(groups.Select(group => (group.GroupKey, group.Name, group.KurinKey)));
    }

    [Fact]
    public async Task Handle_WhenCalledTwiceWithSameKurinKey_ShouldUseCache()
    {
        var kurinKey = Guid.NewGuid();
        _groupRepositoryMock
            .Setup(r => r.GetAllAsync(kurinKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Group>());

        await _handler.Handle(new GetGroupsQuery(kurinKey), CancellationToken.None);
        await _handler.Handle(new GetGroupsQuery(kurinKey), CancellationToken.None);

        _groupRepositoryMock.Verify(r => r.GetAllAsync(kurinKey, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenCalledWithDifferentKurinKeys_ShouldUseDifferentCacheKeys()
    {
        var firstKurinKey = Guid.NewGuid();
        var secondKurinKey = Guid.NewGuid();

        _groupRepositoryMock
            .Setup(r => r.GetAllAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Group>());

        await _handler.Handle(new GetGroupsQuery(firstKurinKey), CancellationToken.None);
        await _handler.Handle(new GetGroupsQuery(secondKurinKey), CancellationToken.None);

        _groupRepositoryMock.Verify(r => r.GetAllAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
    }
}
