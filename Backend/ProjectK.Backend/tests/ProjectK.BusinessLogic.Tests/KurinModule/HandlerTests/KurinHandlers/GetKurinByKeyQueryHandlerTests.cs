using ProjectK.BusinessLogic.Tests.TestHelpers;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AutoMapper;
using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Moq;
using ProjectK.BusinessLogic.MappingProfiles;
using ProjectK.BusinessLogic.Modules.KurinModule.Features.Kurin.Get;
using ProjectK.BusinessLogic.Modules.KurinModule.Models;
using ProjectK.BusinessLogic.Services.Caching;
using ProjectK.Common.Entities.AuthModule;
using ProjectK.Common.Entities.KurinModule;
using ProjectK.Common.Interfaces;
using ProjectK.Common.Interfaces.Modules.AuthModule;
using ProjectK.Common.Interfaces.Modules.KurinModule;
using ProjectK.Common.Models.Enums;
using Xunit;

namespace ProjectK.BusinessLogic.Tests.KurinModule.HandlerTests.KurinHandlers;

public class GetKurinByKeyHandlerTests
{
    private readonly IMapper _mapper;
    private readonly Mock<IKurinRepository> _kurinRepositoryMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly Mock<IAppUserRepository> _appUserRepositoryMock = new();
    private readonly Mock<IMembershipRepository> _membershipRepositoryMock = new();

    private readonly GetKurinByKeyQueryHandler _handler;

    public GetKurinByKeyHandlerTests()
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

        _appUserRepositoryMock
            .Setup(x => x.CountActiveAsync(
                It.IsAny<IReadOnlyCollection<Guid>?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);
        _unitOfWorkMock.Setup(uow => uow.Users).Returns(_appUserRepositoryMock.Object);

        _membershipRepositoryMock
            .Setup(x => x.GetAccountKeysInKurinAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<Guid>());
        _unitOfWorkMock.Setup(uow => uow.Memberships).Returns(_membershipRepositoryMock.Object);

        _handler = new GetKurinByKeyQueryHandler(_unitOfWorkMock.Object, _mapper, CreateCache());
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

        var directlyMapped = _mapper.Map<KurinResponse>(kurin);
        result.Data.Should().BeEquivalentTo(directlyMapped);
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
