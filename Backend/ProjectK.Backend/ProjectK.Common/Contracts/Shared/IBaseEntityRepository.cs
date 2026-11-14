namespace ProjectK.Common.Contracts.Shared;

public interface IBaseEntityRepository<T>
{
    void Create(T entity, CancellationToken cancellationToken = default);
    Task<T?> GetByKeyAsync(Guid entityKey, CancellationToken cancellationToken = default);
    Task<IEnumerable<T>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<bool> ExistsAsync(Guid entityKey, CancellationToken cancellationToken = default);
    void Update(T entity, CancellationToken cancellationToken = default);
    void Delete(T entity, CancellationToken cancellationToken = default);

}
