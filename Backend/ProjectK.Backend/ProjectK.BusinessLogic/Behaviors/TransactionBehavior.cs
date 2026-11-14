using MediatR;
using ProjectK.Common.Contracts.Shared;

namespace ProjectK.BusinessLogic.Behaviors;

/// <summary>
/// Wraps handlers of <see cref="ITransactionalRequest"/> in one database transaction:
/// the handler's own SaveChanges calls enlist in it, commit happens only after the handler
/// returns, and any exception rolls the whole thing back. Requests without the marker
/// (including all queries) pass straight through with no transaction.
/// </summary>
public sealed class TransactionBehavior<TRequest, TResponse>
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly IUnitOfWork _unitOfWork;

    public TransactionBehavior(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        if (request is not ITransactionalRequest)
        {
            return await next(cancellationToken);
        }

        await using var transaction = await _unitOfWork.BeginTransactionAsync(cancellationToken);

        try
        {
            var response = await next(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return response;
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}
