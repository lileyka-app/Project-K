using Microsoft.EntityFrameworkCore.Storage;
using ProjectK.Common.Contracts.Shared;

namespace ProjectK.Infrastructure.UnitOfWork;

/// <summary>
/// Adapts EF Core's transaction to <see cref="IUnitOfWorkTransaction"/>, so the provider type stays
/// inside Infrastructure.
/// </summary>
internal sealed class EfCoreUnitOfWorkTransaction : IUnitOfWorkTransaction
{
    private readonly IDbContextTransaction _transaction;

    public EfCoreUnitOfWorkTransaction(IDbContextTransaction transaction)
    {
        _transaction = transaction;
    }

    public Task CommitAsync(CancellationToken cancellationToken = default)
        => _transaction.CommitAsync(cancellationToken);

    public Task RollbackAsync(CancellationToken cancellationToken = default)
        => _transaction.RollbackAsync(cancellationToken);

    public ValueTask DisposeAsync() => _transaction.DisposeAsync();
}
