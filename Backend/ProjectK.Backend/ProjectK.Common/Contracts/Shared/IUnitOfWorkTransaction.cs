namespace ProjectK.Common.Contracts.Shared;

/// <summary>
/// A database transaction as the callers actually use it: commit, roll back, dispose.
/// <para>
/// <see cref="IUnitOfWork"/> used to hand back EF Core's <c>IDbContextTransaction</c>, which pulled
/// <c>Microsoft.EntityFrameworkCore.Storage</c> into the business layer for the sake of three methods.
/// </para>
/// </summary>
public interface IUnitOfWorkTransaction : IAsyncDisposable
{
    Task CommitAsync(CancellationToken cancellationToken = default);

    Task RollbackAsync(CancellationToken cancellationToken = default);
}
