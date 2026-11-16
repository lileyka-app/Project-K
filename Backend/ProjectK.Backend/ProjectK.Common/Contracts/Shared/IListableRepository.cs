namespace ProjectK.Common.Contracts.Shared;

/// <summary>
/// A table that is read whole: small, or global by nature — kurins, the waitlist, system settings.
/// <para>
/// Not part of <see cref="IBaseEntityRepository{T}"/> on purpose. Most tables belong to a kurin, a
/// гурток or a person, and "all of them" is never a question anyone should ask; their repositories
/// used to inherit the method and throw <see cref="NotSupportedException"/> from it, which the
/// compiler could not catch. A repository offers it only by taking this interface.
/// </para>
/// </summary>
public interface IListableRepository<T> : IBaseEntityRepository<T>
{
    Task<IEnumerable<T>> GetAllAsync(CancellationToken cancellationToken = default);
}
