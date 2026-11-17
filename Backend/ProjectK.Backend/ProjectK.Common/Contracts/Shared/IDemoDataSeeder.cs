namespace ProjectK.Common.Contracts.Shared;

public interface IDemoDataSeeder
{
    Task SeedAsync(CancellationToken cancellationToken = default);
}
