using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using Moq;
using ProjectK.BusinessLogic.Modules.KurinModule.Features.Kurin;
using ProjectK.BusinessLogic.Modules.KurinModule.Services;
using ProjectK.BusinessLogic.Services.Caching;
using ProjectK.BusinessLogic.Tests.TestHelpers;
using ProjectK.Common.Contracts.KurinModule;
using ProjectK.Common.Contracts.NotificationsModule;
using ProjectK.Common.Contracts.Shared;
using ProjectK.Common.Entities.KurinModule;
using ProjectK.Common.Enums.Shared;

namespace ProjectK.BusinessLogic.Tests.KurinModule.HandlerTests.KurinHandlers;

public class GetKurinsHandlerTests
{
    private readonly IBlobReadLinks _links;
    private readonly Mock<IKurinRepository> _kurinRepositoryMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly Mock<IMembershipRepository> _membershipRepositoryMock = new();
    private readonly GetKurinsQueryHandler _handler;

    public GetKurinsHandlerTests()
    {
        _links = FakeBlobReadLinks.Instance;

        _kurinRepositoryMock = new Mock<IKurinRepository>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();

        _unitOfWorkMock.Setup(uow => uow.Kurins).Returns(_kurinRepositoryMock.Object);

        _membershipRepositoryMock
            .Setup(x => x.CountCurrentInKurinsAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, int>());
        _unitOfWorkMock.Setup(uow => uow.Memberships).Returns(_membershipRepositoryMock.Object);

        _handler = new GetKurinsQueryHandler(_unitOfWorkMock.Object, _links, CreateCache(), new MembershipDirectory(_unitOfWorkMock.Object));
    }

    [Fact]
    public async Task Handle_ShouldReportEveryoneWhoBelongsToEachKurinNow_FromOneRead()
    {
        var crowded = new Kurin(1) { KurinKey = Guid.NewGuid() };
        var empty = new Kurin(2) { KurinKey = Guid.NewGuid() };
        _kurinRepositoryMock.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Kurin> { crowded, empty });
        _membershipRepositoryMock
            .Setup(x => x.CountCurrentInKurinsAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, int> { [crowded.KurinKey] = 5 });

        var result = await _handler.Handle(new GetKurinsQuery(), CancellationToken.None);

        result.Data!.Single(kurin => kurin.KurinKey == crowded.KurinKey).CurrentUserCount.Should().Be(5);
        result.Data!.Single(kurin => kurin.KurinKey == empty.KurinKey).CurrentUserCount.Should().Be(0);
        _membershipRepositoryMock.Verify(
            x => x.CountCurrentInKurinsAsync(
                It.Is<IReadOnlyCollection<Guid>>(keys => keys.Count == 2 && keys.Contains(crowded.KurinKey) && keys.Contains(empty.KurinKey)),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    private static IBackendCache CreateCache() =>
        new MemoryBackendCache(new MemoryCache(new MemoryCacheOptions()), Microsoft.Extensions.Logging.Abstractions.NullLogger<MemoryBackendCache>.Instance);

    [Fact]
    public async Task Handle_WhenKurinsExist_ShouldReturnSuccessWithMappedResponses()
    {
        // Arrange
        var kurins = new List<Kurin>
        {
            new Kurin(1) { KurinKey = Guid.NewGuid() },
            new Kurin(2) { KurinKey = Guid.NewGuid() },
            new Kurin(3) { KurinKey = Guid.NewGuid() }
        };

        _kurinRepositoryMock.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(kurins);

        var query = new GetKurinsQuery();

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Type.Should().Be(ResultType.Success);
        result.Data.Should().NotBeNull();
        result.Data.Should().HaveCount(3);

        // Verify each kurin was mapped correctly
        foreach (var kurin in kurins)
        {
            result.Data.Should().Contain(kr =>
                kr.KurinKey == kurin.KurinKey &&
                kr.Number == kurin.Number);
        }

        _kurinRepositoryMock.Verify(r => r.GetAllAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenNoKurinsExist_ShouldReturnSuccess()
    {
        // Arrange
        _kurinRepositoryMock.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Kurin>());

        var query = new GetKurinsQuery();

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Type.Should().Be(ResultType.Success);
        result.Data.Should().BeEmpty();

        _kurinRepositoryMock.Verify(r => r.GetAllAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenRepositoryThrowsException_ShouldPropagateException()
    {
        // Arrange
        var expectedException = new Exception("Database error");

        _kurinRepositoryMock.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(expectedException);

        var query = new GetKurinsQuery();

        // Act & Assert
        var exception = await Assert.ThrowsAsync<Exception>(() =>
            _handler.Handle(query, CancellationToken.None));

        exception.Should().BeSameAs(expectedException);

        _kurinRepositoryMock.Verify(r => r.GetAllAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenMappingWorks_ShouldMapCorrectly()
    {
        // Arrange
        var kurins = new List<Kurin>
        {
            new Kurin(123) { KurinKey = Guid.NewGuid() },
            new Kurin(456) { KurinKey = Guid.NewGuid() }
        };

        _kurinRepositoryMock.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(kurins);

        var query = new GetKurinsQuery();

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Type.Should().Be(ResultType.Success);
        result.Data.Should().NotBeNull();

        result.Data.Select(kurin => (kurin.KurinKey, kurin.Number)).Should().Equal(kurins.Select(kurin => (kurin.KurinKey, kurin.Number)));
    }

    [Fact]
    public async Task Handle_WhenCalledTwice_ShouldUseCache()
    {
        var kurins = new List<Kurin>
        {
            new Kurin(1) { KurinKey = Guid.NewGuid() }
        };

        _kurinRepositoryMock.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(kurins);

        await _handler.Handle(new GetKurinsQuery(), CancellationToken.None);
        await _handler.Handle(new GetKurinsQuery(), CancellationToken.None);

        _kurinRepositoryMock.Verify(r => r.GetAllAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
