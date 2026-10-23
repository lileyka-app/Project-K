using ProjectK.BusinessLogic.Tests.TestHelpers;
using System;
using System.Threading;
using System.Threading.Tasks;
using AutoMapper;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using ProjectK.BusinessLogic.MappingProfiles;
using ProjectK.BusinessLogic.Modules.KurinModule.Features.Kurin.Upsert;
using ProjectK.BusinessLogic.Modules.KurinModule.Models;
using ProjectK.BusinessLogic.Services.Caching;
using ProjectK.Common.Entities.KurinModule;
using ProjectK.Common.Interfaces;
using ProjectK.Common.Interfaces.Modules.InfrastructureModule;
using ProjectK.Common.Interfaces.Modules.KraiModule;
using ProjectK.Common.Interfaces.Modules.KurinModule;
using ProjectK.Common.Models.Audit;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Settings;
using Xunit;

namespace ProjectK.BusinessLogic.Tests.KurinModule.HandlerTests.KurinHandlers;

public class UpsertKurinHandlerTests
{
    private readonly IMapper _mapper;
    private readonly Mock<IKurinRepository> _kurinRepositoryMock;
    private readonly Mock<IKraiRepository> _kraiRepositoryMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly Mock<IBackendCache> _cacheMock;
    private readonly UpsertKurinCommandHandler _handler;
    private readonly JournalRecorder _journal = new(Mock.Of<ICurrentUserContext>());

    public UpsertKurinHandlerTests()
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
        _cacheMock = new Mock<IBackendCache>();

        _kraiRepositoryMock = new Mock<IKraiRepository>();
        _kraiRepositoryMock.Setup(r => r.ExistsAsync(It.IsIn("ua", "cz"), It.IsAny<CancellationToken>())).ReturnsAsync(true);

        _unitOfWorkMock.Setup(uow => uow.Kurins).Returns(_kurinRepositoryMock.Object);
        _unitOfWorkMock.Setup(uow => uow.Krais).Returns(_kraiRepositoryMock.Object);

        _handler = new UpsertKurinCommandHandler(_unitOfWorkMock.Object, _mapper, _cacheMock.Object, Options.Create(new KurinNumberOptions()), Options.Create(new KraiOptions()), _journal.Audit);
    }

    [Fact]
    public async Task Handle_WhenCreatingNewKurin_ShouldCreateAndReturnSuccess()
    {
        // Arrange
        var number = 123;
        var command = new UpsertKurinCommand(number);
        Kurin savedKurin = null!;

        _kurinRepositoryMock.Setup(r => r.Create(It.IsAny<Kurin>(), default))
            .Callback<Kurin, CancellationToken>((k, _) =>
            {
                k.KurinKey.Should().NotBeEmpty();
                k.Number.Should().Be(number);
                savedKurin = k;
            });

        _unitOfWorkMock.Setup(u => u.SaveChangesAsync(default)).ReturnsAsync(1);

        // Act
        var result = await _handler.Handle(command, default);

        // Assert
        result.Type.Should().Be(ResultType.Created);
        result.Data.Should().NotBeNull();
        result.Data.Number.Should().Be(number);
        result.Data.KurinKey.Should().Be(savedKurin.KurinKey);

        _kurinRepositoryMock.Verify(r => r.Create(It.IsAny<Kurin>(), default), Times.Once);
        _kurinRepositoryMock.Verify(r => r.Update(It.IsAny<Kurin>(), default), Times.Never);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(default), Times.Once);
        _cacheMock.Verify(c => c.Invalidate(BackendCachePolicies.KurinReads), Times.Once);
        _cacheMock.Verify(c => c.Invalidate(BackendCachePolicies.GroupReads), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenUpdatingExistingKurin_ShouldUpdateAndReturnSuccess()
    {
        // Arrange
        var kurinKey = Guid.NewGuid();
        var oldNumber = 123;
        var newNumber = 156;
        var existingKurin = new Kurin(oldNumber) { KurinKey = kurinKey };
        var command = new UpsertKurinCommand(kurinKey, newNumber, "  Kyiv  ", "  Ukraine  ", "  Some Patron  ", "  Long form notes  ", profileVerificationEnabled: true);

        _kurinRepositoryMock.Setup(r => r.GetByKeyAsync(kurinKey, default))
            .ReturnsAsync(existingKurin);

        _unitOfWorkMock.Setup(u => u.SaveChangesAsync(default)).ReturnsAsync(1);

        // Act
        var result = await _handler.Handle(command, default);

        // Assert
        result.Type.Should().Be(ResultType.Success);
        result.Data.Should().NotBeNull();
        result.Data.Number.Should().Be(newNumber);
        result.Data.KurinKey.Should().Be(kurinKey);

        existingKurin.Number.Should().Be(newNumber);
        existingKurin.Stanytsia.Should().Be("Kyiv");
        existingKurin.RegionOrCountry.Should().Be("Ukraine");
        existingKurin.NamedAfter.Should().Be("Some Patron");
        existingKurin.Description.Should().Be("Long form notes");
        existingKurin.ProfileVerificationEnabled.Should().BeTrue();
        result.Data.ProfileVerificationEnabled.Should().BeTrue();

        _kurinRepositoryMock.Verify(r => r.Update(existingKurin, default), Times.Once);
        _kurinRepositoryMock.Verify(r => r.Create(It.IsAny<Kurin>(), default), Times.Never);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(default), Times.Once);
    }

    [Theory]
    [InlineData(121, null, null, "StanytsiaTooLong")]
    [InlineData(null, 121, null, "RegionOrCountryTooLong")]
    [InlineData(null, null, 201, "NamedAfterTooLong")]
    [InlineData(null, null, 4001, "DescriptionTooLong")]
    public async Task Handle_WhenProfileFieldsAreTooLong_ShouldReturnBadRequest(
        int? stanytsiaLength,
        int? regionLength,
        int? namedAfterOrDescriptionLength,
        string expectedErrorCode)
    {
        // Arrange
        var command = new UpsertKurinCommand(
            Guid.NewGuid(),
            123,
            stanytsiaLength.HasValue ? new string('a', stanytsiaLength.Value) : null,
            regionLength.HasValue ? new string('a', regionLength.Value) : null,
            expectedErrorCode == "NamedAfterTooLong" ? new string('a', namedAfterOrDescriptionLength!.Value) : null,
            expectedErrorCode == "DescriptionTooLong" ? new string('a', namedAfterOrDescriptionLength!.Value) : null);

        // Act
        var result = await _handler.Handle(command, default);

        // Assert
        result.Type.Should().Be(ResultType.BadRequest);
        result.ErrorCode.Should().Be(expectedErrorCode);
        _kurinRepositoryMock.Verify(r => r.GetByKeyAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenSaveChangesFails_ShouldReturnInternalServerError()
    {
        // Arrange
        var number = 123;
        var command = new UpsertKurinCommand(number);

        _unitOfWorkMock.Setup(u => u.SaveChangesAsync(default)).ReturnsAsync(0);

        // Act
        var result = await _handler.Handle(command, default);

        // Assert
        result.Type.Should().Be(ResultType.InternalServerError);
        result.Data.Should().BeNull();

        _kurinRepositoryMock.Verify(r => r.Create(It.IsAny<Kurin>(), default), Times.Once);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(default), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenRepositoryThrowsException_ShouldPropagateException()
    {
        // Arrange
        var kurinKey = Guid.NewGuid();
        var number = 123;
        var command = new UpsertKurinCommand(kurinKey, number);
        var expectedException = new Exception("Database error");

        _kurinRepositoryMock.Setup(r => r.GetByKeyAsync(kurinKey, default))
            .ThrowsAsync(expectedException);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<Exception>(() => _handler.Handle(command, default));
        exception.Should().BeSameAs(expectedException);
    }

    [Fact]
    public async Task Handle_WhenMappingWorks_ShouldMapCorrectly()
    {
        // Arrange
        var number = 123;
        var command = new UpsertKurinCommand(number);
        Kurin savedKurin = null!;

        _kurinRepositoryMock.Setup(r => r.Create(It.IsAny<Kurin>(), default))
            .Callback<Kurin, CancellationToken>((k, _) => savedKurin = k);

        _unitOfWorkMock.Setup(u => u.SaveChangesAsync(default)).ReturnsAsync(1);

        // Act
        var result = await _handler.Handle(command, default);

        // Assert
        var directlyMapped = _mapper.Map<KurinResponse>(savedKurin);
        result.Data.Should().BeEquivalentTo(directlyMapped);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-3)]
    [InlineData(201)]
    [InlineData(1153)]
    public async Task Handle_WhenNewNumberIsOutsideTheRange_ShouldReturnBadRequest(int number)
    {
        var result = await _handler.Handle(new UpsertKurinCommand(number), default);

        result.Type.Should().Be(ResultType.BadRequest);
        result.ErrorCode.Should().Be("KurinNumberOutOfRange");
        _kurinRepositoryMock.Verify(r => r.Create(It.IsAny<Kurin>(), default), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenTheRangeIsRaised_ShouldAcceptTheHigherNumber()
    {
        var handler = new UpsertKurinCommandHandler(
            _unitOfWorkMock.Object, _mapper, _cacheMock.Object, Options.Create(new KurinNumberOptions { MaxNumber = 350 }), Options.Create(new KraiOptions()), _journal.Audit);
        _unitOfWorkMock.Setup(u => u.SaveChangesAsync(default)).ReturnsAsync(1);

        var result = await handler.Handle(new UpsertKurinCommand(300), default);

        result.Type.Should().Be(ResultType.Created);
        result.Data!.Number.Should().Be(300);
    }

    [Fact]
    public async Task Handle_WhenAKurinKeepsANumberOutsideTheRange_ShouldStillUpdate()
    {
        var kurinKey = Guid.NewGuid();
        var testKurin = new Kurin(0) { KurinKey = kurinKey };
        _kurinRepositoryMock.Setup(r => r.GetByKeyAsync(kurinKey, default)).ReturnsAsync(testKurin);
        _unitOfWorkMock.Setup(u => u.SaveChangesAsync(default)).ReturnsAsync(1);

        var result = await _handler.Handle(new UpsertKurinCommand(kurinKey, 0, "Lviv"), default);

        result.Type.Should().Be(ResultType.Success);
        testKurin.Number.Should().Be(0);
        testKurin.Stanytsia.Should().Be("Lviv");

        var entry = _journal.Entries.Should().ContainSingle().Subject;
        entry.Area.Should().Be(AuditArea.Settings);
        entry.Action.Should().Be(AuditActions.Settings.KurinChanged);
        entry.KurinKey.Should().Be(kurinKey);
        entry.Changes.Should().Equal(new AuditChange("stanytsia", null, "Lviv"));
    }

    [Fact]
    public async Task Handle_WhenTheVerificationFlagAndNumberChange_RecordsBoth_AndNothingWhenResent()
    {
        var kurinKey = Guid.NewGuid();
        var kurin = new Kurin(47) { KurinKey = kurinKey };
        _kurinRepositoryMock.Setup(r => r.GetByKeyAsync(kurinKey, default)).ReturnsAsync(kurin);
        _unitOfWorkMock.Setup(u => u.SaveChangesAsync(default)).ReturnsAsync(1);

        await _handler.Handle(new UpsertKurinCommand(kurinKey, 48, profileVerificationEnabled: true), default);
        await _handler.Handle(new UpsertKurinCommand(kurinKey, 48, profileVerificationEnabled: true), default);

        _journal.Entries.Should().ContainSingle().Which.Changes.Should().Equal(
            new AuditChange("number", "47", "48"),
            new AuditChange("profileVerificationEnabled", "false", "true"));
    }

    [Fact]
    public async Task Handle_WhenPreparatoryKurinTakesAFreeNumber_ShouldStopBeingPreparatory()
    {
        var kurinKey = Guid.NewGuid();
        var preparatory = new Kurin(null) { KurinKey = kurinKey };
        _kurinRepositoryMock.Setup(r => r.GetByKeyAsync(kurinKey, default)).ReturnsAsync(preparatory);
        _unitOfWorkMock.Setup(u => u.SaveChangesAsync(default)).ReturnsAsync(1);

        var result = await _handler.Handle(new UpsertKurinCommand(kurinKey, 47), default);

        result.Type.Should().Be(ResultType.Success);
        result.Data!.IsPreparatory.Should().BeFalse();
        preparatory.Number.Should().Be(47);
    }

    [Fact]
    public async Task Handle_WhenPreparatoryKurinTakesATakenNumber_ShouldReturnConflict()
    {
        var kurinKey = Guid.NewGuid();
        var preparatory = new Kurin(null) { KurinKey = kurinKey };
        _kurinRepositoryMock.Setup(r => r.GetByKeyAsync(kurinKey, default)).ReturnsAsync(preparatory);
        _kurinRepositoryMock.Setup(r => r.GetByNumberAsync("ua", 47, default)).ReturnsAsync(new Kurin(47));

        var result = await _handler.Handle(new UpsertKurinCommand(kurinKey, 47), default);

        result.Type.Should().Be(ResultType.Conflict);
        result.ErrorCode.Should().Be("KurinNumberTaken");
        preparatory.Number.Should().BeNull();
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenNumberIsCleared_ShouldMakeTheKurinPreparatory()
    {
        var kurinKey = Guid.NewGuid();
        var kurin = new Kurin(47) { KurinKey = kurinKey };
        _kurinRepositoryMock.Setup(r => r.GetByKeyAsync(kurinKey, default)).ReturnsAsync(kurin);
        _unitOfWorkMock.Setup(u => u.SaveChangesAsync(default)).ReturnsAsync(1);

        var result = await _handler.Handle(new UpsertKurinCommand(kurinKey, null), default);

        result.Type.Should().Be(ResultType.Success);
        result.Data!.IsPreparatory.Should().BeTrue();
        kurin.Number.Should().BeNull();
    }

    [Fact]
    public async Task Handle_WhenCreatingWithoutAKrai_ShouldUseTheDefault()
    {
        Kurin savedKurin = null!;
        _kurinRepositoryMock.Setup(r => r.Create(It.IsAny<Kurin>(), default))
            .Callback<Kurin, CancellationToken>((k, _) => savedKurin = k);
        _unitOfWorkMock.Setup(u => u.SaveChangesAsync(default)).ReturnsAsync(1);
        var handler = new UpsertKurinCommandHandler(
            _unitOfWorkMock.Object, _mapper, _cacheMock.Object, Options.Create(new KurinNumberOptions()), Options.Create(new KraiOptions { Default = "cz" }), _journal.Audit);

        var result = await handler.Handle(new UpsertKurinCommand(12), default);

        result.Type.Should().Be(ResultType.Created);
        savedKurin.KraiCode.Should().Be("cz");
        result.Data!.KraiCode.Should().Be("cz");
    }

    [Fact]
    public async Task Handle_WhenTheNumberIsTakenInAnotherKrai_ShouldStillCreate()
    {
        _kurinRepositoryMock.Setup(r => r.GetByNumberAsync("ua", 12, default)).ReturnsAsync(new Kurin(12));
        _unitOfWorkMock.Setup(u => u.SaveChangesAsync(default)).ReturnsAsync(1);

        var result = await _handler.Handle(new UpsertKurinCommand(12, "cz"), default);

        result.Type.Should().Be(ResultType.Created);
        result.Data!.KraiCode.Should().Be("cz");
        _kurinRepositoryMock.Verify(r => r.GetByNumberAsync("cz", 12, default), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenTheKraiIsUnknown_ShouldReturnBadRequest()
    {
        var result = await _handler.Handle(new UpsertKurinCommand(12, "xx"), default);

        result.Type.Should().Be(ResultType.BadRequest);
        result.ErrorCode.Should().Be("UnknownKrai");
        _kurinRepositoryMock.Verify(r => r.Create(It.IsAny<Kurin>(), default), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenUpdating_ShouldCheckTheNumberWithinTheKurinsOwnKrai()
    {
        var kurinKey = Guid.NewGuid();
        var kurin = new Kurin(5) { KurinKey = kurinKey, KraiCode = "cz" };
        _kurinRepositoryMock.Setup(r => r.GetByKeyAsync(kurinKey, default)).ReturnsAsync(kurin);
        _kurinRepositoryMock.Setup(r => r.GetByNumberAsync("ua", 47, default)).ReturnsAsync(new Kurin(47));
        _unitOfWorkMock.Setup(u => u.SaveChangesAsync(default)).ReturnsAsync(1);

        var result = await _handler.Handle(new UpsertKurinCommand(kurinKey, 47), default);

        result.Type.Should().Be(ResultType.Success);
        kurin.Number.Should().Be(47);
        kurin.KraiCode.Should().Be("cz");
    }
}
