using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ProjectK.Common.Entities.AgendaModule;
using ProjectK.Common.Entities.KurinModule;

namespace ProjectK.Infrastructure.Persistence.Configurations.AgendaModule;

internal sealed class AgendaCategoryConfiguration : IEntityTypeConfiguration<AgendaCategory>
{
    public void Configure(EntityTypeBuilder<AgendaCategory> builder)
    {
        builder.HasKey(e => e.AgendaCategoryKey);
        builder.Property(e => e.Name).HasMaxLength(100).IsRequired();
        builder.Property(e => e.ColorHex).HasMaxLength(32).IsRequired();
        builder.Property(e => e.Icon).HasMaxLength(64);
        builder.Property(e => e.DefaultDescription).HasMaxLength(2000);
        // Cascade: an event group belongs to its kurin and dies with it (so the seeder's kurin reset
        // and any kurin delete clear categories automatically). The item→category side is NoAction to
        // keep this the only cascade path to AgendaItems.
        builder.HasOne<Kurin>()
            .WithMany()
            .HasForeignKey(e => e.KurinKey)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(e => new { e.KurinKey, e.IsArchived });
    }
}
