using ProjectK.BusinessLogic.Tests.TestHelpers;
using AutoMapper;
using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Moq;
using ProjectK.BusinessLogic.MappingProfiles;
using ProjectK.BusinessLogic.Modules.KurinModule.Features.Kurin.Get;
using ProjectK.BusinessLogic.Modules.KurinModule.Models;
using ProjectK.BusinessLogic.Services.Caching;
using ProjectK.Common.Entities.KurinModule;
using ProjectK.Common.Interfaces;
using ProjectK.Common.Interfaces.Modules.KurinModule;
using ProjectK.Common.Models.Enums;

namespace ProjectK.BusinessLogic.Tests.KurinModule.HandlerTests.KurinHandlers;

public class GetKurinsHandlerTests
{
    private readonly IMapper _mapper;
    private readonly Mock<IKurinRepository> _kurinRepositoryMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly GetKurinsQueryHandler _handler;

    public GetKurinsHandlerTests()
    {
        var loggerFactory = LoggerFactory.Create(builder => { });
        var config = new MapperConfiguration(cfg =>
        {
            cfg.ConstructServicesUsing(FakeBlobReadLinks.Resolvers);
            cfg.AddProfile(new KurinModuleProfile());
        }, loggerFactory);
        _mapper = config.CreateMapper();

        _kurinRepositoryMock = new Mock<IKurinRepository>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();

        _unitOfWorkMock.Setup(uow => uow.Kurins).Returns(_kurinRepositoryMock.Object);

        _handler = new GetKurinsQueryHandler(_unitOfWorkMock.Object, _mapper, CreateCache());
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

        // Compare with direct mapping
        var directlyMapped = _mapper.Map<IEnumerable<KurinResponse>>(kurins);
        result.Data.Should().BeEquivalentTo(directlyMapped);
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
