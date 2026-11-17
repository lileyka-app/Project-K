using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using ProjectK.Common.Contracts.Shared;
using ProjectK.Infrastructure.DbContexts;

namespace ProjectK.Infrastructure.Repositories;

/// <summary>
/// The CRUD half of every repository. <see cref="IBaseEntityRepository{T}"/> had been declared since
/// the start but never implemented, so each of the nineteen repositories spelled the same six methods
/// out again against its own <see cref="DbSet{TEntity}"/>.
/// <para>
/// The key column is read from EF's model rather than passed in per repository, so entities keep their
/// own key names (<c>MemberKey</c>, <c>AgendaCategoryKey</c>, …) without the base needing to know them.
/// </para>
/// <para>
/// Everything is <c>virtual</c>: repositories that eager-load related data or narrow the query
/// override just that member.
/// </para>
/// <para>
/// Reading the whole table is not here: a repository that offers it takes
/// <see cref="IListableRepository{T}"/> and writes it, so the rest have no method to refuse.
/// </para>
/// </summary>
public abstract class BaseEntityRepository<T> : IBaseEntityRepository<T>
    where T : class
{
    protected BaseEntityRepository(AppDbContext context)
    {
        Context = context;
    }

    protected AppDbContext Context { get; }

    protected DbSet<T> Set => Context.Set<T>();

    public virtual void Create(T entity, CancellationToken cancellationToken = default) => Set.Add(entity);

    public virtual void Update(T entity, CancellationToken cancellationToken = default) => Set.Update(entity);

    public virtual void Delete(T entity, CancellationToken cancellationToken = default) => Set.Remove(entity);

    public virtual Task<T?> GetByKeyAsync(Guid entityKey, CancellationToken cancellationToken = default) =>
        Set.FirstOrDefaultAsync(KeyEquals(entityKey), cancellationToken);

    public virtual Task<bool> ExistsAsync(Guid entityKey, CancellationToken cancellationToken = default) =>
        Set.AnyAsync(KeyEquals(entityKey), cancellationToken);

    /// <summary>
    /// Marks an entity modified whether or not the context is already tracking it.
    /// <para>
    /// Not the default: <see cref="DbSet{TEntity}.Update"/> marks the whole graph modified, while this
    /// touches only the root. Repositories that were written this way keep it by overriding
    /// <see cref="Update"/>; switching the rest over would quietly change what gets written.
    /// </para>
    /// </summary>
    protected void MarkModified(T entity)
    {
        var entry = Context.Entry(entity);
        if (entry.State == EntityState.Detached)
        {
            Set.Update(entity);
            return;
        }

        entry.State = EntityState.Modified;
    }

    /// <summary>Matches the entity whose primary key equals <paramref name="entityKey"/>.</summary>
    private Expression<Func<T, bool>> KeyEquals(Guid entityKey)
    {
        var keyName = PrimaryKeyName;
        return entity => EF.Property<Guid>(entity, keyName) == entityKey;
    }

    private string PrimaryKeyName
    {
        get
        {
            var key = Context.Model.FindEntityType(typeof(T))?.FindPrimaryKey()
                ?? throw new InvalidOperationException($"{typeof(T).Name} has no primary key in the EF model.");

            return key.Properties.Count == 1
                ? key.Properties[0].Name
                : throw new InvalidOperationException(
                    $"{typeof(T).Name} has a composite primary key; its repository must override the key-based members.");
        }
    }
}
