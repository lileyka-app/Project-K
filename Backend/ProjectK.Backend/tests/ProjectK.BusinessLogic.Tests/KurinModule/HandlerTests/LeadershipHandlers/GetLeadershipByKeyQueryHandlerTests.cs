using ProjectK.BusinessLogic.Tests.TestHelpers;
using AutoMapper;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using ProjectK.BusinessLogic.MappingProfiles;
using ProjectK.BusinessLogic.Modules.KurinModule.Features.Leadership;
using ProjectK.BusinessLogic.Modules.KurinModule.Models;
using ProjectK.Common.Entities.KurinModule;
using ProjectK.Common.Interfaces;
using ProjectK.Common.Interfaces.Modules.KurinModule;
using ProjectK.Common.Models.Enums;

namespace ProjectK.BusinessLogic.Tests.KurinModule.HandlerTests.LeadershipHandlers;

public class GetLeadershipByKeyHandlerTests
{
    private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();
    private readonly Mock<ILeadershipRepository> _leadershipRepoMock = new();
    // The real profile: EntityKey is derived by the mapping, so a stubbed IMapper
    // would assert the stub rather than the behaviour.
    private readonly IMapper _mapper = new MapperConfiguration(
        cfg =>
        {
            cfg.ConstructServicesUsing(FakeBlobReadLinks.Resolvers);
            cfg.AddProfile(new KurinModuleProfile());
        },
        NullLoggerFactory.Instance).CreateMapper();
    private readonly GetLeadershipByKeyQueryHandler _handler;

    public GetLeadershipByKeyHandlerTests()
    {
        _unitOfWorkMock.Setup(u => u.Leaderships).Returns(_leadershipRepoMock.Object);
        _handler = new GetLeadershipByKeyQueryHandler(_unitOfWorkMock.Object, _mapper);
    }

    private static Leadership BuildLeadership(LeadershipType type, Guid? kurinKey = null, Guid? groupKey = null) =>
        new()
        {
            LeadershipKey = Guid.NewGuid(),
            Type = type,
            KurinKey = kurinKey,
            GroupKey = groupKey,
            StartDate = new DateOnly(2024, 1, 1)
        };

    private static LeadershipResponse BuildDto(Leadership entity) =>
        new()
        {
            LeadershipKey = entity.LeadershipKey,
            Type = entity.Type,
            KurinKey = entity.KurinKey,
            GroupKey = entity.GroupKey,
            StartDate = entity.StartDate,
            EndDate = entity.EndDate,
            LeadershipHistories = []
        };

    [Fact]
    public async Task Handle_ShouldReturnNotFound_WhenEntityDoesNotExist()
    {
        var query = new GetLeadershipByKeyQuery(Guid.NewGuid());

        _leadershipRepoMock
            .Setup(r => r.GetByKeyAsync(query.LeadershipKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Leadership?)null);

        var result = await _handler.Handle(query, CancellationToken.None);

        Assert.Equal(ResultType.NotFound, result.Type);
        Assert.Null(result.Data);
    }

    [Fact]
    public async Task Handle_ShouldMapAndReturnSuccess_ForKurinType()
    {
        var entity = BuildLeadership(LeadershipType.Kurin, kurinKey: Guid.NewGuid());
        var query = new GetLeadershipByKeyQuery(entity.LeadershipKey);

        _leadershipRepoMock
            .Setup(r => r.GetByKeyAsync(entity.LeadershipKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(entity);


        var result = await _handler.Handle(query, CancellationToken.None);

        Assert.Equal(ResultType.Success, result.Type);
        Assert.NotNull(result.Data);
        Assert.Equal(entity.LeadershipKey, result.Data!.LeadershipKey);
        Assert.Equal(entity.KurinKey, result.Data.EntityKey); // Kurin maps to KurinKey
        Assert.Equal(LeadershipType.Kurin, result.Data.Type);
    }

    [Fact]
    public async Task Handle_ShouldMapAndReturnSuccess_ForKvType()
    {
        var entity = BuildLeadership(LeadershipType.KV, kurinKey: Guid.NewGuid());
        var query = new GetLeadershipByKeyQuery(entity.LeadershipKey);

        _leadershipRepoMock
            .Setup(r => r.GetByKeyAsync(entity.LeadershipKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(entity);


        var result = await _handler.Handle(query, CancellationToken.None);

        Assert.Equal(ResultType.Success, result.Type);
        Assert.NotNull(result.Data);
        Assert.Equal(entity.KurinKey, result.Data!.EntityKey); // KV treated like Kurin
        Assert.Equal(LeadershipType.KV, result.Data.Type);
    }

    [Fact]
    public async Task Handle_ShouldMapAndReturnSuccess_ForGroupType()
    {
        var entity = BuildLeadership(LeadershipType.Group, groupKey: Guid.NewGuid());
        var query = new GetLeadershipByKeyQuery(entity.LeadershipKey);

        _leadershipRepoMock
            .Setup(r => r.GetByKeyAsync(entity.LeadershipKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(entity);


        var result = await _handler.Handle(query, CancellationToken.None);

        Assert.Equal(ResultType.Success, result.Type);
        Assert.NotNull(result.Data);
        Assert.Equal(entity.GroupKey, result.Data!.EntityKey);
        Assert.Equal(LeadershipType.Group, result.Data.Type);
    }

    [Fact]
    public async Task Handle_ShouldSetEntityKeyToEmpty_WhenKurinTypeHasNullKurinKey()
    {
        var entity = BuildLeadership(LeadershipType.Kurin, kurinKey: null);
        var query = new GetLeadershipByKeyQuery(entity.LeadershipKey);

        _leadershipRepoMock
            .Setup(r => r.GetByKeyAsync(entity.LeadershipKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(entity);


        var result = await _handler.Handle(query, CancellationToken.None);

        Assert.Equal(ResultType.Success, result.Type);
        Assert.Equal(Guid.Empty, result.Data!.EntityKey);
    }

    [Fact]
    public async Task Handle_ShouldSetEntityKeyToEmpty_WhenGroupTypeHasNullGroupKey()
    {
        var entity = BuildLeadership(LeadershipType.Group, groupKey: null);
        var query = new GetLeadershipByKeyQuery(entity.LeadershipKey);

        _leadershipRepoMock
            .Setup(r => r.GetByKeyAsync(entity.LeadershipKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(entity);


        var result = await _handler.Handle(query, CancellationToken.None);

        Assert.Equal(ResultType.Success, result.Type);
        Assert.Equal(Guid.Empty, result.Data!.EntityKey);
    }

    [Fact]
    public async Task Handle_ShouldPassCancellationToken_ToRepository()
    {
        var entity = BuildLeadership(LeadershipType.KV, kurinKey: Guid.NewGuid());
        var query = new GetLeadershipByKeyQuery(entity.LeadershipKey);
        using var cts = new CancellationTokenSource();

        _leadershipRepoMock
            .Setup(r => r.GetByKeyAsync(entity.LeadershipKey, cts.Token))
            .ReturnsAsync(entity);


        var result = await _handler.Handle(query, cts.Token);

        Assert.Equal(ResultType.Success, result.Type);
        _leadershipRepoMock.Verify(r => r.GetByKeyAsync(entity.LeadershipKey, cts.Token), Times.Once);
    }
}
