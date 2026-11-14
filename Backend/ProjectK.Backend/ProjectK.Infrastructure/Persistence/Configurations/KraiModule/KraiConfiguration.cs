using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ProjectK.Common.Entities.KraiModule;
using ProjectK.Infrastructure.DbContexts;

namespace ProjectK.Infrastructure.Persistence.Configurations.KraiModule;

internal sealed class KraiConfiguration : IEntityTypeConfiguration<Krai>
{
    public void Configure(EntityTypeBuilder<Krai> builder)
    {
        builder.HasKey(e => e.Code);
        builder.HasData(KraiSeed.All.Select(seed => new Krai { Code = seed.Code, Name = seed.Name, CreatedAtUtc = seed.SeededAtUtc }));
    }
}
