using Moq;
using ProjectK.BusinessLogic.Modules.AuthModule.Features.Access;
using ProjectK.Common.Contracts.InfrastructureModule;
using ProjectK.Common.Enums.AuthModule;
using ProjectK.Common.Enums.Shared;
using ProjectK.Common.Records.InfrastructureModule;

namespace ProjectK.BusinessLogic.Tests.AuthModule.HandlerTests.CheckEntityAccess;

public class CheckEntityAccessQueryHandlerTests
{
    private readonly Mock<IResourceAccessService> _resourceAccessServiceMock;
    private readonly CheckEntityAccessQueryHandler _handler;

    public CheckEntityAccessQueryHandlerTests()
    {
        _resourceAccessServiceMock = new Mock<IResourceAccessService>();
        _handler = new CheckEntityAccessQueryHandler(_resourceAccessServiceMock.Object);
    }

    [Fact]
    public async Task Handle_ShouldReturnTrue_WhenResourceAccessServiceAllows()
    {
        var memberKey = Guid.NewGuid();
        var query = new CheckEntityAccessQuery
        {
            EntityType = "member",
            EntityKey = memberKey.ToString(),
            ActiveKurinKey = Guid.NewGuid().ToString() // should be ignored
        };

        _resourceAccessServiceMock
            .Setup(x => x.CheckAccessAsync(ResourceType.Member, ResourceAction.Read, memberKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ResourceAccessDecision.Allow());

        var result = await _handler.Handle(query, CancellationToken.None);

        Assert.Equal(ResultType.Success, result.Type);
        Assert.True(result.Data);

        _resourceAccessServiceMock.Verify(x =>
            x.CheckAccessAsync(ResourceType.Member, ResourceAction.Read, memberKey, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_ShouldReturnFalse_WhenResourceAccessServiceDenies()
    {
        var groupKey = Guid.NewGuid();
        var query = new CheckEntityAccessQuery
        {
            EntityType = "group",
            EntityKey = groupKey.ToString(),
            ActiveKurinKey = Guid.NewGuid().ToString() // should be ignored
        };

        _resourceAccessServiceMock
            .Setup(x => x.CheckAccessAsync(ResourceType.Group, ResourceAction.Read, groupKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ResourceAccessDecision.Deny("Different scope."));

        var result = await _handler.Handle(query, CancellationToken.None);

        Assert.Equal(ResultType.Success, result.Type);
        Assert.False(result.Data);
    }

    [Fact]
    public async Task Handle_ShouldReturnBadRequest_WhenEntityTypeIsInvalid()
    {
        var entityKey = Guid.NewGuid();
        var query = new CheckEntityAccessQuery
        {
            EntityType = "invalid",
            EntityKey = entityKey.ToString(),
            ActiveKurinKey = Guid.NewGuid().ToString()
        };

        var result = await _handler.Handle(query, CancellationToken.None);

        Assert.Equal(ResultType.BadRequest, result.Type);
        Assert.False(result.Data);

        _resourceAccessServiceMock.Verify(x =>
            x.CheckAccessAsync(It.IsAny<ResourceType>(), It.IsAny<ResourceAction>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_ShouldReturnBadRequest_WhenEntityKeyIsInvalidGuid()
    {
        var query = new CheckEntityAccessQuery
        {
            EntityType = "member",
            EntityKey = "not-a-guid",
            ActiveKurinKey = Guid.NewGuid().ToString()
        };

        var result = await _handler.Handle(query, CancellationToken.None);

        Assert.Equal(ResultType.BadRequest, result.Type);
        Assert.False(result.Data);

        _resourceAccessServiceMock.Verify(x =>
            x.CheckAccessAsync(It.IsAny<ResourceType>(), It.IsAny<ResourceAction>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_ShouldMapKurinAliasKv_ToKurinResourceType()
    {
        var kurinKey = Guid.NewGuid();
        var query = new CheckEntityAccessQuery
        {
            EntityType = "kv",
            EntityKey = kurinKey.ToString(),
            ActiveKurinKey = Guid.NewGuid().ToString()
        };

        _resourceAccessServiceMock
            .Setup(x => x.CheckAccessAsync(ResourceType.Kurin, ResourceAction.Read, kurinKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ResourceAccessDecision.Allow());

        var result = await _handler.Handle(query, CancellationToken.None);

        Assert.Equal(ResultType.Success, result.Type);
        Assert.True(result.Data);

        _resourceAccessServiceMock.Verify(x =>
            x.CheckAccessAsync(ResourceType.Kurin, ResourceAction.Read, kurinKey, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public void Constructor_ShouldInitializeResourceAccessServiceCorrectly()
    {
        var handler = new CheckEntityAccessQueryHandler(_resourceAccessServiceMock.Object);
        Assert.NotNull(handler);
    }
}
