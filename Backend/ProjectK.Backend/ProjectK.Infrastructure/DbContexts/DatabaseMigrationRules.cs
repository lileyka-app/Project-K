using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ProjectK.Infrastructure.DbContexts;

/// <summary>
/// Applies pending EF migrations at startup, so the host never has to reach for the context itself.
/// </summary>
public static class DatabaseMigrationRules
{
    /// <summary>Migrates the database of the <see cref="AppDbContext"/> resolved from <paramref name="services"/>.</summary>
    public static Task ApplyAsync(IServiceProvider services) =>
        services.GetRequiredService<AppDbContext>().Database.MigrateAsync();
}
