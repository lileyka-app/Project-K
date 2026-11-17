using Moq;
using ProjectK.BusinessLogic.Modules.IdentityModule.Features.TileLayout;
using ProjectK.Common.Contracts.IdentityModule;
using ProjectK.Common.Contracts.Shared;
using ProjectK.Common.Entities.IdentityModule;
using ProjectK.Common.Enums.Shared;
using ProjectK.Common.Rules.IdentityModule;

namespace ProjectK.BusinessLogic.Tests.IdentityModule.HandlerTests.TileLayout;

public class TileLayoutHandlersTests
{
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly Mock<IUserTileLayoutRepository> _repositoryMock;

    public TileLayoutHandlersTests()
    {
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _repositoryMock = new Mock<IUserTileLayoutRepository>();
        _unitOfWorkMock.Setup(u => u.UserTileLayouts).Returns(_repositoryMock.Object);
        _unitOfWorkMock.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
    }

    [Fact]
    public async Task Save_ShouldCreate_WhenNoExistingLayout()
    {
        var userKey = Guid.NewGuid();
        _repositoryMock
            .Setup(r => r.GetByBoardAsync(userKey, TileBoardNames.MemberCard, It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserTileLayout?)null);

        UserTileLayout? created = null;
        _repositoryMock
            .Setup(r => r.Create(It.IsAny<UserTileLayout>(), It.IsAny<CancellationToken>()))
            .Callback<UserTileLayout, CancellationToken>((e, _) => created = e);

        var handler = new SaveTileLayoutCommandHandler(_unitOfWorkMock.Object);
        var result = await handler.Handle(
            new SaveTileLayoutCommand(userKey, TileBoardNames.MemberCard, new[] { "profile", "skills", "probes" }, Array.Empty<string>(), 1),
            CancellationToken.None);

        Assert.Equal(ResultType.Success, result.Type);
        Assert.NotNull(created);
        Assert.Equal("[\"profile\",\"skills\",\"probes\"]", created!.TileOrderJson);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Save_ShouldUpdate_WhenLayoutExists()
    {
        var userKey = Guid.NewGuid();
        var existing = new UserTileLayout
        {
            UserTileLayoutKey = Guid.NewGuid(),
            UserKey = userKey,
            BoardKey = TileBoardNames.MemberCard,
            TileOrderJson = "[\"profile\"]",
            SchemaVersion = 1
        };
        _repositoryMock
            .Setup(r => r.GetByBoardAsync(userKey, TileBoardNames.MemberCard, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        var handler = new SaveTileLayoutCommandHandler(_unitOfWorkMock.Object);
        var result = await handler.Handle(
            new SaveTileLayoutCommand(userKey, TileBoardNames.MemberCard, new[] { "skills", "profile" }, Array.Empty<string>(), 1),
            CancellationToken.None);

        Assert.Equal(ResultType.Success, result.Type);
        Assert.Equal("[\"skills\",\"profile\"]", existing.TileOrderJson);
        _repositoryMock.Verify(r => r.Update(existing, It.IsAny<CancellationToken>()), Times.Once);
        _repositoryMock.Verify(r => r.Create(It.IsAny<UserTileLayout>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Save_ShouldNormalizeNonPositiveSchemaVersionToOne()
    {
        var userKey = Guid.NewGuid();
        _repositoryMock
            .Setup(r => r.GetByBoardAsync(userKey, TileBoardNames.MemberCard, It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserTileLayout?)null);

        UserTileLayout? created = null;
        _repositoryMock
            .Setup(r => r.Create(It.IsAny<UserTileLayout>(), It.IsAny<CancellationToken>()))
            .Callback<UserTileLayout, CancellationToken>((e, _) => created = e);

        var handler = new SaveTileLayoutCommandHandler(_unitOfWorkMock.Object);
        await handler.Handle(
            new SaveTileLayoutCommand(userKey, TileBoardNames.MemberCard, new[] { "profile" }, Array.Empty<string>(), 0),
            CancellationToken.None);

        Assert.Equal(1, created!.SchemaVersion);
    }

    [Fact]
    public async Task Get_ShouldReturnOnlyKnownBoards()
    {
        var userKey = Guid.NewGuid();
        _repositoryMock
            .Setup(r => r.GetByUserAsync(userKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<UserTileLayout>
            {
                new() { BoardKey = TileBoardNames.MemberCard, TileOrderJson = "[\"profile\",\"skills\"]", SchemaVersion = 1 },
                new() { BoardKey = "legacy-removed-board", TileOrderJson = "[\"x\"]", SchemaVersion = 1 }
            });

        var handler = new GetTileLayoutsQueryHandler(_unitOfWorkMock.Object);
        var result = await handler.Handle(new GetTileLayoutsQuery(userKey), CancellationToken.None);

        Assert.Equal(ResultType.Success, result.Type);
        Assert.NotNull(result.Data);
        Assert.Single(result.Data!);
        var dto = result.Data!.Single();
        Assert.Equal(TileBoardNames.MemberCard, dto.BoardKey);
        Assert.Equal(new[] { "profile", "skills" }, dto.TileKeys);
    }

    [Fact]
    public async Task Get_ShouldDegradeCorruptedJsonToEmptyOrder()
    {
        var userKey = Guid.NewGuid();
        _repositoryMock
            .Setup(r => r.GetByUserAsync(userKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<UserTileLayout>
            {
                new() { BoardKey = TileBoardNames.MemberCard, TileOrderJson = "{ this is not json", SchemaVersion = 1 }
            });

        var handler = new GetTileLayoutsQueryHandler(_unitOfWorkMock.Object);
        var result = await handler.Handle(new GetTileLayoutsQuery(userKey), CancellationToken.None);

        Assert.Equal(ResultType.Success, result.Type);
        Assert.Empty(result.Data!.Single().TileKeys);
    }

    [Fact]
    public async Task Reset_ShouldDelete_WhenLayoutExists()
    {
        var userKey = Guid.NewGuid();
        var existing = new UserTileLayout { BoardKey = TileBoardNames.MemberCard };
        _repositoryMock
            .Setup(r => r.GetByBoardAsync(userKey, TileBoardNames.MemberCard, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        var handler = new ResetTileLayoutCommandHandler(_unitOfWorkMock.Object);
        var result = await handler.Handle(new ResetTileLayoutCommand(userKey, TileBoardNames.MemberCard), CancellationToken.None);

        Assert.Equal(ResultType.Success, result.Type);
        _repositoryMock.Verify(r => r.Delete(existing, It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Reset_ShouldSucceed_WhenNoLayoutExists()
    {
        var userKey = Guid.NewGuid();
        _repositoryMock
            .Setup(r => r.GetByBoardAsync(userKey, TileBoardNames.MemberCard, It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserTileLayout?)null);

        var handler = new ResetTileLayoutCommandHandler(_unitOfWorkMock.Object);
        var result = await handler.Handle(new ResetTileLayoutCommand(userKey, TileBoardNames.MemberCard), CancellationToken.None);

        Assert.Equal(ResultType.Success, result.Type);
        _repositoryMock.Verify(r => r.Delete(It.IsAny<UserTileLayout>(), It.IsAny<CancellationToken>()), Times.Never);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public void Mapping_RoundTripsTheKeys()
    {
        var keys = new[] { "profile", "skills", "probes" };
        var json = TileLayoutMapping.WriteKeys(keys);
        Assert.Equal(keys, TileLayoutMapping.ReadKeys(json));
    }

    [Fact]
    public void ApplyTo_WritesOnlyTheLayoutFields()
    {
        var owner = Guid.NewGuid();
        var key = Guid.NewGuid();
        var now = new DateTime(2026, 11, 14, 10, 0, 0, DateTimeKind.Utc);
        var layout = new UserTileLayout
        {
            UserTileLayoutKey = key,
            UserKey = owner,
            BoardKey = TileBoardNames.MemberCard,
            TileOrderJson = "[\"old\"]",
            HiddenTilesJson = "[]",
            SchemaVersion = 1,
            UpdatedAtUtc = now.AddDays(-1)
        };
        var request = new SaveTileLayoutCommand(Guid.NewGuid(), "another-board", ["profile", "skills"], ["probes"], 0);

        request.ApplyTo(layout, now);

        Assert.Equal(key, layout.UserTileLayoutKey);
        Assert.Equal(owner, layout.UserKey);
        Assert.Equal(TileBoardNames.MemberCard, layout.BoardKey);
        Assert.Equal(["profile", "skills"], TileLayoutMapping.ReadKeys(layout.TileOrderJson));
        Assert.Equal(["probes"], TileLayoutMapping.ReadKeys(layout.HiddenTilesJson));
        Assert.Equal(1, layout.SchemaVersion);
        Assert.Equal(now, layout.UpdatedAtUtc);
    }
}
