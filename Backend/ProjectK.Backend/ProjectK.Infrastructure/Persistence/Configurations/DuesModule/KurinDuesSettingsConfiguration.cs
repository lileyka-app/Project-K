using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ProjectK.Common.Entities.DuesModule;

namespace ProjectK.Infrastructure.Persistence.Configurations.DuesModule;

/// <summary>Held by key with no foreign key, as every dues table: see <see cref="KurinDuesRateConfiguration"/>.</summary>
internal sealed class KurinDuesSettingsConfiguration : IEntityTypeConfiguration<KurinDuesSettings>
{
    public void Configure(EntityTypeBuilder<KurinDuesSettings> builder)
    {
        builder.HasKey(e => e.KurinDuesSettingsKey);
        builder.Property(e => e.ChargeTiming).HasConversion<int>();
        builder.HasIndex(e => e.KurinKey).IsUnique();
    }
}
