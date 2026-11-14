using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using ProjectK.Infrastructure.Persistence.Interceptors;

namespace ProjectK.Infrastructure.DbContexts;

public sealed class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")
            ?? Environment.GetEnvironmentVariable("DefaultConnection")
            ?? "Server=(localdb)\\mssqllocaldb;Database=ProjectK.DesignTime;Trusted_Connection=True;MultipleActiveResultSets=true";

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer(connectionString, builder => builder.MigrationsAssembly("ProjectK.Infrastructure"))
            .AddAppDbContextInterceptors()
            .Options;

        return new AppDbContext(options);
    }
}
