using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using Moq;
using ProjectK.BusinessLogic.Modules.KurinModule.Features.Kurin;
using ProjectK.BusinessLogic.Modules.KurinModule.Services;
using ProjectK.BusinessLogic.Services.Caching;
using ProjectK.BusinessLogic.Tests.TestHelpers;
using ProjectK.Common.Contracts.KurinModule;
using ProjectK.Common.Contracts.Shared;
using ProjectK.Common.Entities.KurinModule;
using ProjectK.Common.Enums.Shared;

namespace ProjectK.BusinessLogic.Tests.KurinModule.HandlerTests.KurinHandlers;

public class GetKurinByKeyHandlerTests
{
    private readonly IBlobReadLinks _links;
    private readonly Mock<IKurinRepository> _kurinRepositoryMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly Mock<IMembershipRepository> _membershipRepositoryMock = new();

    private readonly GetKurinByKeyQueryHandler _handler;

    public GetKurinByKeyHandlerTests()
    {
        _links = FakeBlobReadLinks.Instance;

        _kurinRepositoryMock = new Mock<IKurinRepository>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();

        _unitOfWorkMock.Setup(uow => uow.Kurins).Returns(_kurinRepositoryMock.Object);

        _membershipRepositoryMock
            .Setup(x => x.CountCurrentInKurinAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);
        _unitOfWorkMock.Setup(uow => uow.Memberships).Returns(_membershipRepositoryMock.Object);

        _handler = new GetKurinByKeyQueryHandler(_unitOfWorkMock.Object, _links, CreateCache(), new MembershipDirectory(_unitOfWorkMock.Object));
    }

    private static IBackendCache CreateCache() =>
        new MemoryBackendCache(new MemoryCache(new MemoryCacheOptions()), Microsoft.Extensions.Logging.Abstractions.NullLogger<MemoryBackendCache>.Instance);

    [Fact]
    public async Task Handle_WhenKurinExists_ShouldReturnSuccessWithMappedResponse()
    {
        // Arrange
        var kurinKey = Guid.NewGuid();
        var kurin = new Kurin(123) { KurinKey = kurinKey };
        var query = new GetKurinByKeyQuery(kurinKey);

        _kurinRepositoryMock.Setup(r => r.GetByKeyAsync(kurinKey, default))
            .ReturnsAsync(kurin);

        // Act
        var result = await _handler.Handle(query, default);

        // Assert
        result.Type.Should().Be(ResultType.Success);
        result.Data.Should().NotBeNull();
        result.Data.KurinKey.Should().Be(kurinKey);
        result.Data.Number.Should().Be(123);

        _kurinRepositoryMock.Verify(r => r.GetByKeyAsync(kurinKey, default), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenKurinDoesNotExist_ShouldReturnNotFound()
    {
        // Arrange
        var kurinKey = Guid.NewGuid();
        var query = new GetKurinByKeyQuery(kurinKey);

        _kurinRepositoryMock.Setup(r => r.GetByKeyAsync(kurinKey, default))
            .ReturnsAsync((Kurin)null!);

        // Act
        var result = await _handler.Handle(query, default);

        // Assert
        result.Type.Should().Be(ResultType.NotFound);
        result.Data.Should().BeNull();

        _kurinRepositoryMock.Verify(r => r.GetByKeyAsync(kurinKey, default), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenRepositoryThrowsException_ShouldPropagateException()
    {
        // Arrange
        var kurinKey = Guid.NewGuid();
        var query = new GetKurinByKeyQuery(kurinKey);
        var expectedException = new Exception("Database error");

        _kurinRepositoryMock.Setup(r => r.GetByKeyAsync(kurinKey, default))
            .ThrowsAsync(expectedException);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<Exception>(() => _handler.Handle(query, default));
        exception.Should().BeSameAs(expectedException);

        _kurinRepositoryMock.Verify(r => r.GetByKeyAsync(kurinKey, default), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenMappingWorks_ShouldMapCorrectly()
    {
        // Arrange
        var kurinKey = Guid.NewGuid();
        var kurin = new Kurin(456) { KurinKey = kurinKey };
        var query = new GetKurinByKeyQuery(kurinKey);

        _kurinRepositoryMock.Setup(r => r.GetByKeyAsync(kurinKey, default))
            .ReturnsAsync(kurin);

        // Act
        var result = await _handler.Handle(query, default);

        // Assert
        result.Type.Should().Be(ResultType.Success);
        result.Data.Should().NotBeNull();
        result.Data.KurinKey.Should().Be(kurinKey);
        result.Data.Number.Should().Be(456);

        result.Data.Should().BeEquivalentTo(new { KurinKey = kurinKey, Number = 456, kurin.KraiCode, kurin.Branch, CurrentUserCount = 0, EmblemUrl = (string?)null, IsPreparatory = false });
    }

    [Fact]
    public async Task Handle_ShouldReportEveryoneWhoBelongsToTheKurinNow()
    {
        var kurinKey = Guid.NewGuid();
        _kurinRepositoryMock.Setup(r => r.GetByKeyAsync(kurinKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Kurin(456) { KurinKey = kurinKey });
        _membershipRepositoryMock
            .Setup(x => x.CountCurrentInKurinAsync(kurinKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(7);

        var result = await _handler.Handle(new GetKurinByKeyQuery(kurinKey), default);

        result.Data!.CurrentUserCount.Should().Be(7);
    }

    [Fact]
    public async Task Handle_WhenCalledTwiceWithSameKurinKey_ShouldUseCache()
    {
        var kurinKey = Guid.NewGuid();
        var kurin = new Kurin(456) { KurinKey = kurinKey };

        _kurinRepositoryMock.Setup(r => r.GetByKeyAsync(kurinKey, default))
            .ReturnsAsync(kurin);

        await _handler.Handle(new GetKurinByKeyQuery(kurinKey), default);
        await _handler.Handle(new GetKurinByKeyQuery(kurinKey), default);

        _kurinRepositoryMock.Verify(r => r.GetByKeyAsync(kurinKey, default), Times.Once);
    }
}
