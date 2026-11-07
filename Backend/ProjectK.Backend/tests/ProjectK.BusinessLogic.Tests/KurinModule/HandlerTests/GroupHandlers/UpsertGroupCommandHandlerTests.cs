using ProjectK.BusinessLogic.Tests.TestHelpers;
using System;
using System.Threading;
using System.Threading.Tasks;
using AutoMapper;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using ProjectK.BusinessLogic.MappingProfiles;
using ProjectK.BusinessLogic.Modules.KurinModule.Features.Group.Upsert;
using ProjectK.BusinessLogic.Modules.KurinModule.Models;
using ProjectK.BusinessLogic.Services.Caching;
using ProjectK.Common.Entities.KurinModule;
using ProjectK.Common.Interfaces;
using ProjectK.Common.Interfaces.Modules.KurinModule;
using ProjectK.Common.Models.Enums;
using Xunit;

namespace ProjectK.BusinessLogic.Tests.KurinModule.HandlerTests.GroupHandlers;

public class UpsertGroupHandlerTests
{
    private readonly IMapper _mapper;
    private readonly Mock<IGroupRepository> _groupRepositoryMock;
    private readonly Mock<IKurinRepository> _kurinRepositoryMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly Mock<IBackendCache> _cacheMock;
    private readonly UpsertGroupCommandHandler _handler;

    public UpsertGroupHandlerTests()
    {
        var loggerFactory = LoggerFactory.Create(builder => { });
        var mapperConfig = new MapperConfiguration(cfg =>
        {
            cfg.ConstructServicesUsing(FakeBlobReadLinks.Resolvers);
            cfg.AddProfile(new KurinModuleProfile());
        }, loggerFactory);
        _mapper = mapperConfig.CreateMapper();

        _groupRepositoryMock = new Mock<IGroupRepository>();
        _kurinRepositoryMock = new Mock<IKurinRepository>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _cacheMock = new Mock<IBackendCache>();

        _unitOfWorkMock.Setup(u => u.Groups).Returns(_groupRepositoryMock.Object);
        _unitOfWorkMock.Setup(u => u.Kurins).Returns(_kurinRepositoryMock.Object);

        _handler = new UpsertGroupCommandHandler(_unitOfWorkMock.Object, _mapper, _cacheMock.Object);
    }

    [Fact]
    public async Task Handle_WhenCreatingNewGroup_ShouldCreateAndReturnCreated()
    {
        // Arrange
        var kurin = new Kurin(5) { KurinKey = Guid.NewGuid() };
        var name = "Alpha";
        var description = "  First patrol group  ";
        Group savedGroup = null!;

        var command = new UpsertGroupCommand(name, kurin.KurinKey, description);

        _groupRepositoryMock
            .Setup(r => r.GetByKeyAsync(Guid.Empty, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Group)null!);

        _kurinRepositoryMock
            .Setup(r => r.GetByKeyAsync(kurin.KurinKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(kurin);

        _groupRepositoryMock
            .Setup(r => r.Create(It.IsAny<Group>(), It.IsAny<CancellationToken>()))
            .Callback<Group, CancellationToken>((g, _) =>
            {
                // emulate setting nav property (handler currently doesn't)
                g.Kurin = kurin;
                savedGroup = g;
            });

        _unitOfWorkMock
            .Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Type.Should().Be(ResultType.Created);
        result.Data.Should().NotBeNull();
        result.Data!.Name.Should().Be(name);
        result.Data.Description.Should().Be("First patrol group");
        _cacheMock.Verify(c => c.Invalidate(BackendCachePolicies.GroupReads), Times.Once);
        result.Data.KurinKey.Should().Be(kurin.KurinKey);
        result.Data.GroupKey.Should().Be(savedGroup.GroupKey);

        _groupRepositoryMock.Verify(r => r.Create(It.IsAny<Group>(), It.IsAny<CancellationToken>()), Times.Once);
        _groupRepositoryMock.Verify(r => r.Update(It.IsAny<Group>(), It.IsAny<CancellationToken>()), Times.Never);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ShouldRefuseAGroup_InASeniorKurin()
    {
        var senior = new Kurin(5, KurinBranch.USP);
        _groupRepositoryMock
            .Setup(r => r.GetByKeyAsync(Guid.Empty, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Group)null!);
        _kurinRepositoryMock
            .Setup(r => r.GetByKeyAsync(senior.KurinKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(senior);

        var result = await _handler.Handle(new UpsertGroupCommand("Alpha", senior.KurinKey), CancellationToken.None);

        result.Type.Should().Be(ResultType.BadRequest);
        result.ErrorCode.Should().Be("NoGroupsInSeniorKurin");
        _groupRepositoryMock.Verify(r => r.Create(It.IsAny<Group>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenKurinDoesNotExistOnCreate_ShouldReturnNotFound()
    {
        // Arrange
        var kurinKey = Guid.NewGuid();
        var command = new UpsertGroupCommand("Alpha", kurinKey);

        _groupRepositoryMock
            .Setup(r => r.GetByKeyAsync(Guid.Empty, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Group)null!);

        _kurinRepositoryMock
            .Setup(r => r.GetByKeyAsync(kurinKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Kurin)null!);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Type.Should().Be(ResultType.NotFound);
        result.Data.Should().BeNull();
        _groupRepositoryMock.Verify(r => r.Create(It.IsAny<Group>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenUpdatingExistingGroup_ShouldUpdateAndReturnSuccess()
    {
        // Arrange
        var kurin = new Kurin(10) { KurinKey = Guid.NewGuid() };
        var groupKey = Guid.NewGuid();
        var existing = new Group("OldName", kurin.KurinKey) { GroupKey = groupKey, Kurin = kurin };
        var newName = "NewName";

        var command = new UpsertGroupCommand(groupKey, newName, "  Updated group description  ");

        _groupRepositoryMock
            .Setup(r => r.GetByKeyAsync(groupKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        // (Handler will also call Kurins.GetByKeyAsync with request.KurinKey which is default Guid.Empty in update ctor)
        _kurinRepositoryMock
            .Setup(r => r.GetByKeyAsync(Guid.Empty, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Kurin)null!);

        _unitOfWorkMock
            .Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Type.Should().Be(ResultType.Success);
        result.Data.Should().NotBeNull();
        result.Data!.GroupKey.Should().Be(groupKey);
        result.Data.Name.Should().Be(newName);
        result.Data.Description.Should().Be("Updated group description");
        existing.Name.Should().Be(newName);
        existing.Description.Should().Be("Updated group description");

        _groupRepositoryMock.Verify(r => r.Update(existing, It.IsAny<CancellationToken>()), Times.Once);
        _groupRepositoryMock.Verify(r => r.Create(It.IsAny<Group>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenDescriptionIsTooLong_ShouldReturnBadRequest()
    {
        // Arrange
        var command = new UpsertGroupCommand("Alpha", Guid.NewGuid(), new string('a', 1001));

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Type.Should().Be(ResultType.BadRequest);
        result.ErrorCode.Should().Be("DescriptionTooLong");
        _groupRepositoryMock.Verify(r => r.GetByKeyAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenSaveChangesFailsOnCreate_ShouldReturnInternalServerError()
    {
        // Arrange
        var kurin = new Kurin(3) { KurinKey = Guid.NewGuid() };
        var command = new UpsertGroupCommand("Bravo", kurin.KurinKey);

        _groupRepositoryMock
            .Setup(r => r.GetByKeyAsync(Guid.Empty, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Group)null!);

        _kurinRepositoryMock
            .Setup(r => r.GetByKeyAsync(kurin.KurinKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(kurin);

        _unitOfWorkMock
            .Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Type.Should().Be(ResultType.InternalServerError);
        result.Data.Should().BeNull();
        _groupRepositoryMock.Verify(r => r.Create(It.IsAny<Group>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenRepositoryThrows_ShouldPropagate()
    {
        // Arrange
        var groupKey = Guid.NewGuid();
        var command = new UpsertGroupCommand(groupKey, "Name");
        var expected = new Exception("DB failure");

        _groupRepositoryMock
            .Setup(r => r.GetByKeyAsync(groupKey, It.IsAny<CancellationToken>()))
            .ThrowsAsync(expected);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<Exception>(() => _handler.Handle(command, CancellationToken.None));
        ex.Should().BeSameAs(expected);
    }

    [Fact]
    public async Task Handle_WhenMappingWorks_ShouldMapCorrectly()
    {
        // Arrange
        var kurin = new Kurin(9) { KurinKey = Guid.NewGuid() };
        var command = new UpsertGroupCommand("Echo", kurin.KurinKey);
        Group saved = null!;

        _groupRepositoryMock
            .Setup(r => r.GetByKeyAsync(Guid.Empty, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Group)null!);

        _kurinRepositoryMock
            .Setup(r => r.GetByKeyAsync(kurin.KurinKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(kurin);

        _groupRepositoryMock
            .Setup(r => r.Create(It.IsAny<Group>(), It.IsAny<CancellationToken>()))
            .Callback<Group, CancellationToken>((g, _) =>
            {
                g.Kurin = kurin;
                saved = g;
            });

        _unitOfWorkMock
            .Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Type.Should().Be(ResultType.Created);
        var mapped = _mapper.Map<GroupResponse>(saved);
        result.Data.Should().BeEquivalentTo(mapped);
    }
    /// <summary>
    /// Renaming a гурток must not move it. The update command carries no kurin at all, so a
    /// mapping that copied it would write Guid.Empty over the foreign key — which the database
    /// rejects, turning every group edit into a 500.
    /// </summary>
    [Fact]
    public async Task Handle_WhenUpdatingAGroup_ShouldLeaveItsKurinAlone()
    {
        var kurinKey = Guid.NewGuid();
        var existing = new Group("Before", kurinKey, "old")
        {
            GroupKey = Guid.NewGuid()
        };

        _groupRepositoryMock
            .Setup(r => r.GetByKeyAsync(existing.GroupKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);
        _unitOfWorkMock
            .Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var result = await _handler.Handle(
            new UpsertGroupCommand(existing.GroupKey, "After", "new"),
            CancellationToken.None);

        result.Type.Should().Be(ResultType.Success);
        existing.Name.Should().Be("After");
        existing.KurinKey.Should().Be(kurinKey);
    }

}
