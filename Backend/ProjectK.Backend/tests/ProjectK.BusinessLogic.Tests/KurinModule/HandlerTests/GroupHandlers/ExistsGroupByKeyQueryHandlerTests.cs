using Moq;
using ProjectK.BusinessLogic.Modules.KurinModule.Features.Group;
using ProjectK.Common.Contracts.KurinModule;
using ProjectK.Common.Contracts.Shared;
using ProjectK.Common.Models.Enums;

namespace ProjectK.BusinessLogic.Tests.KurinModule.HandlerTests.GroupHandlers;

public class ExistsGroupByKeyHandlerTests
{
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly Mock<IGroupRepository> _groupRepositoryMock;
    private readonly ExistsGroupByKeyQueryHandler _handler;

    public ExistsGroupByKeyHandlerTests()
    {
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _groupRepositoryMock = new Mock<IGroupRepository>();

        _unitOfWorkMock.Setup(x => x.Groups).Returns(_groupRepositoryMock.Object);

        _handler = new ExistsGroupByKeyQueryHandler(_unitOfWorkMock.Object);
    }

    [Fact]
    public async Task Handle_ShouldReturnSuccessWithTrue_WhenGroupExists()
    {
        // Arrange
        var groupKey = Guid.NewGuid();
        var query = new ExistsGroupByKeyQuery(groupKey);

        _groupRepositoryMock.Setup(x => x.ExistsAsync(groupKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.Equal(ResultType.Success, result.Type);
        Assert.True(result.Data);
        _groupRepositoryMock.Verify(x => x.ExistsAsync(groupKey, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ShouldReturnSuccessWithFalse_WhenGroupDoesNotExist()
    {
        // Arrange
        var groupKey = Guid.NewGuid();
        var query = new ExistsGroupByKeyQuery(groupKey);

        _groupRepositoryMock.Setup(x => x.ExistsAsync(groupKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.Equal(ResultType.Success, result.Type);
        Assert.False(result.Data);
        _groupRepositoryMock.Verify(x => x.ExistsAsync(groupKey, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ShouldReturnSuccessWithFalse_WhenGroupKeyIsEmpty()
    {
        // Arrange
        var emptyGroupKey = Guid.Empty;
        var query = new ExistsGroupByKeyQuery(emptyGroupKey);

        _groupRepositoryMock.Setup(x => x.ExistsAsync(emptyGroupKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.Equal(ResultType.Success, result.Type);
        Assert.False(result.Data);
        _groupRepositoryMock.Verify(x => x.ExistsAsync(emptyGroupKey, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ShouldPassCorrectCancellationToken()
    {
        // Arrange
        var groupKey = Guid.NewGuid();
        var query = new ExistsGroupByKeyQuery(groupKey);
        var cancellationToken = new CancellationToken();

        _groupRepositoryMock.Setup(x => x.ExistsAsync(groupKey, cancellationToken))
            .ReturnsAsync(true);

        // Act
        var result = await _handler.Handle(query, cancellationToken);

        // Assert
        Assert.Equal(ResultType.Success, result.Type);
        Assert.True(result.Data);
        _groupRepositoryMock.Verify(x => x.ExistsAsync(groupKey, cancellationToken), Times.Once);
    }

    [Fact]
    public async Task Handle_ShouldHandleMultipleCallsCorrectly()
    {
        // Arrange
        var groupKey1 = Guid.NewGuid();
        var groupKey2 = Guid.NewGuid();
        var query1 = new ExistsGroupByKeyQuery(groupKey1);
        var query2 = new ExistsGroupByKeyQuery(groupKey2);

        _groupRepositoryMock.Setup(x => x.ExistsAsync(groupKey1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _groupRepositoryMock.Setup(x => x.ExistsAsync(groupKey2, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // Act
        var result1 = await _handler.Handle(query1, CancellationToken.None);
        var result2 = await _handler.Handle(query2, CancellationToken.None);

        // Assert
        Assert.Equal(ResultType.Success, result1.Type);
        Assert.True(result1.Data);

        Assert.Equal(ResultType.Success, result2.Type);
        Assert.False(result2.Data);

        _groupRepositoryMock.Verify(x => x.ExistsAsync(groupKey1, It.IsAny<CancellationToken>()), Times.Once);
        _groupRepositoryMock.Verify(x => x.ExistsAsync(groupKey2, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ShouldReturnSuccessResultType_Regardless_OfExistenceResult()
    {
        // Arrange
        var groupKey = Guid.NewGuid();
        var query = new ExistsGroupByKeyQuery(groupKey);

        // Test both true and false scenarios to ensure ResultType is always Success
        _groupRepositoryMock.SetupSequence(x => x.ExistsAsync(groupKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true)
            .ReturnsAsync(false);

        // Act
        var result1 = await _handler.Handle(query, CancellationToken.None);
        var result2 = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.Equal(ResultType.Success, result1.Type);
        Assert.Equal(ResultType.Success, result2.Type);
    }

    [Fact]
    public async Task Handle_ShouldSetCorrectDataProperty()
    {
        // Arrange
        var groupKey = Guid.NewGuid();
        var query = new ExistsGroupByKeyQuery(groupKey);

        _groupRepositoryMock.Setup(x => x.ExistsAsync(groupKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.IsType<bool>(result.Data);
        Assert.True(result.Data);
    }

    [Fact]
    public async Task Handle_ShouldUseCorrectRepository()
    {
        // Arrange
        var groupKey = Guid.NewGuid();
        var query = new ExistsGroupByKeyQuery(groupKey);

        _groupRepositoryMock.Setup(x => x.ExistsAsync(groupKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // Act
        await _handler.Handle(query, CancellationToken.None);

        // Assert
        _unitOfWorkMock.Verify(x => x.Groups, Times.Once);
        _groupRepositoryMock.Verify(x => x.ExistsAsync(groupKey, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ShouldWorkWithDifferentGuidValues()
    {
        // Arrange
        var testCases = new[]
        {
            Guid.NewGuid(),
            Guid.Empty,
            new Guid("12345678-1234-1234-1234-123456789012"),
            new Guid("FFFFFFFF-FFFF-FFFF-FFFF-FFFFFFFFFFFF")
        };

        foreach (var groupKey in testCases)
        {
            var query = new ExistsGroupByKeyQuery(groupKey);
            _groupRepositoryMock.Setup(x => x.ExistsAsync(groupKey, It.IsAny<CancellationToken>()))
                .ReturnsAsync(groupKey != Guid.Empty);

            // Act
            var result = await _handler.Handle(query, CancellationToken.None);

            // Assert
            Assert.Equal(ResultType.Success, result.Type);
            Assert.Equal(groupKey != Guid.Empty, result.Data);
        }

        // Verify all calls were made
        foreach (var groupKey in testCases)
        {
            _groupRepositoryMock.Verify(x => x.ExistsAsync(groupKey, It.IsAny<CancellationToken>()), Times.Once);
        }
    }

    [Fact]
    public void Constructor_ShouldInitializeUnitOfWorkCorrectly()
    {
        // Arrange & Act
        var handler = new ExistsGroupByKeyQueryHandler(_unitOfWorkMock.Object);

        // Assert
        Assert.NotNull(handler);
    }

    [Fact]
    public async Task Handle_ShouldNotThrowException_WhenRepositoryReturnsValidResult()
    {
        // Arrange
        var groupKey = Guid.NewGuid();
        var query = new ExistsGroupByKeyQuery(groupKey);

        _groupRepositoryMock.Setup(x => x.ExistsAsync(groupKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act & Assert
        var exception = await Record.ExceptionAsync(() => _handler.Handle(query, CancellationToken.None));
        Assert.Null(exception);
    }

    [Fact]
    public async Task Handle_ShouldWorkWithCancelledToken()
    {
        // Arrange
        var groupKey = Guid.NewGuid();
        var query = new ExistsGroupByKeyQuery(groupKey);
        var cancelledToken = new CancellationToken(true);

        _groupRepositoryMock.Setup(x => x.ExistsAsync(groupKey, cancelledToken))
            .ThrowsAsync(new OperationCanceledException());

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(
            () => _handler.Handle(query, cancelledToken));
    }
}
