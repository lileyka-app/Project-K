using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ProjectK.Common.Entities.KurinModule.Tags;
using ProjectK.Common.Rules.KurinModule;

namespace ProjectK.Infrastructure.Persistence.Configurations.KurinModule.Tags;

internal sealed class KurinTagConfiguration : IEntityTypeConfiguration<KurinTag>
{
    public void Configure(EntityTypeBuilder<KurinTag> builder)
    {
        builder.HasKey(e => e.KurinTagKey);
        builder.Property(e => e.Name).HasMaxLength(KurinTagRules.MaxNameLength).IsRequired();
        builder.Property(e => e.ColorHex).HasMaxLength(16).IsRequired();
        builder.HasOne(e => e.Kurin)
            .WithMany()
            .HasForeignKey(e => e.KurinKey)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(e => new { e.KurinKey, e.Name }).IsUnique();
    }
}
