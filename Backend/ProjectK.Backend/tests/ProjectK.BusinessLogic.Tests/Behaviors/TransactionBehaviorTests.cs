using FluentAssertions;
using MediatR;
using Moq;
using ProjectK.BusinessLogic.Behaviors;
using ProjectK.Common.Contracts.Shared;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Records;

namespace ProjectK.BusinessLogic.Tests.Behaviors;

public class TransactionBehaviorTests
{
    public sealed record PlainRequest : IRequest<ServiceResult<bool>>;
    public sealed record TransactionalRequest : IRequest<ServiceResult<bool>>, ITransactionalRequest;

    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly Mock<IUnitOfWorkTransaction> _transaction = new();

    public TransactionBehaviorTests()
    {
        _unitOfWork
            .Setup(x => x.BeginTransactionAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(_transaction.Object);
    }

    [Fact]
    public async Task Handle_DoesNotOpenTransaction_WhenRequestIsNotTransactional()
    {
        var behavior = new TransactionBehavior<PlainRequest, ServiceResult<bool>>(_unitOfWork.Object);
        var expected = new ServiceResult<bool>(ResultType.Success, true);

        var result = await behavior.Handle(new PlainRequest(), _ => Task.FromResult(expected), CancellationToken.None);

        result.Should().BeSameAs(expected);
        _unitOfWork.Verify(x => x.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_Commits_WhenTransactionalHandlerSucceeds()
    {
        var behavior = new TransactionBehavior<TransactionalRequest, ServiceResult<bool>>(_unitOfWork.Object);
        var expected = new ServiceResult<bool>(ResultType.Success, true);

        var result = await behavior.Handle(new TransactionalRequest(), _ => Task.FromResult(expected), CancellationToken.None);

        result.Should().BeSameAs(expected);
        _transaction.Verify(x => x.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
        _transaction.Verify(x => x.RollbackAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_RollsBackAndRethrows_WhenHandlerThrows()
    {
        var behavior = new TransactionBehavior<TransactionalRequest, ServiceResult<bool>>(_unitOfWork.Object);

        var act = async () => await behavior.Handle(
            new TransactionalRequest(),
            _ => throw new InvalidOperationException("boom"),
            CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
        _transaction.Verify(x => x.RollbackAsync(It.IsAny<CancellationToken>()), Times.Once);
        _transaction.Verify(x => x.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}
