using System.Reflection;
using Microsoft.EntityFrameworkCore;
using ProjectK.Infrastructure.DbContexts;
using ProjectK.Infrastructure.Repositories;

namespace ProjectK.Infrastructure.Tests;

/// <summary>
/// <see cref="BaseEntityRepository{T}"/> resolves the key column from EF's model and reads it as a
/// <see cref="Guid"/>. That assumption is invisible at compile time, so it is pinned here: a new entity
/// with a composite or non-Guid key would otherwise only fail once a request hit it.
/// </summary>
public class BaseEntityRepositoryModelTests
{
    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new AppDbContext(options);
    }

    /// <summary>Entity types whose repository relies on the base key-based members.</summary>
    public static TheoryData<Type> EntitiesUsingBaseKeyLookup()
    {
        var data = new TheoryData<Type>();

        foreach (var repository in typeof(BaseEntityRepository<>).Assembly.GetTypes())
        {
            if (repository.IsAbstract || !repository.IsClass)
            {
                continue;
            }

            var baseType = repository.BaseType;
            if (baseType is null || !baseType.IsGenericType || baseType.GetGenericTypeDefinition() != typeof(BaseEntityRepository<>))
            {
                continue;
            }

            var inheritsKeyLookup =
                InheritsFromBase(repository, nameof(BaseEntityRepository<object>.GetByKeyAsync)) ||
                InheritsFromBase(repository, nameof(BaseEntityRepository<object>.ExistsAsync));

            if (inheritsKeyLookup)
            {
                data.Add(baseType.GetGenericArguments()[0]);
            }
        }

        return data;
    }

    private static bool InheritsFromBase(Type repository, string methodName)
    {
        var method = repository.GetMethod(
            methodName,
            BindingFlags.Public | BindingFlags.Instance,
            binder: null,
            types: [typeof(Guid), typeof(CancellationToken)],
            modifiers: null);

        return method?.DeclaringType is { IsGenericType: true } declaring
               && declaring.GetGenericTypeDefinition() == typeof(BaseEntityRepository<>);
    }

    [Theory]
    [MemberData(nameof(EntitiesUsingBaseKeyLookup))]
    public void EntityUsingBaseKeyLookup_HasSingleGuidPrimaryKey(Type entityType)
    {
        using var context = CreateContext();

        var primaryKey = context.Model.FindEntityType(entityType)?.FindPrimaryKey();

        Assert.True(primaryKey != null, $"{entityType.Name} is queried by key through the base repository.");
        Assert.True(primaryKey!.Properties.Count == 1, $"{entityType.Name} must be looked up by a single key column.");
        Assert.True(primaryKey.Properties[0].ClrType == typeof(Guid),
            $"{entityType.Name} is read as EF.Property<Guid> by the base repository, but its key is {primaryKey.Properties[0].ClrType.Name}.");
    }

    [Fact]
    public void EveryBaseRepositoryEntity_IsMappedInTheModel()
    {
        Assert.NotEmpty(EntitiesUsingBaseKeyLookup());
    }
}
