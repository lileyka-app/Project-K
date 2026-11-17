using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ProjectK.Common.Entities.DuesModule;

namespace ProjectK.Infrastructure.Persistence.Configurations.DuesModule;

internal sealed class DuesEntryEventConfiguration : IEntityTypeConfiguration<DuesEntryEvent>
{
    public void Configure(EntityTypeBuilder<DuesEntryEvent> builder)
    {
        builder.HasKey(e => e.DuesEntryEventKey);
        builder.Property(e => e.Action).HasMaxLength(50).IsRequired();
        builder.HasOne(e => e.DuesEntry)
            .WithMany(e => e.Events)
            .HasForeignKey(e => e.DuesEntryKey)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
