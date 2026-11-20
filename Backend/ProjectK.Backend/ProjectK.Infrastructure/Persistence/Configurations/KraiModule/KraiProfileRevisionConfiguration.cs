using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ProjectK.Common.Entities.KraiModule;
using ProjectK.Infrastructure.DbContexts;

namespace ProjectK.Infrastructure.Persistence.Configurations.KraiModule;

internal sealed class KraiProfileRevisionConfiguration : IEntityTypeConfiguration<KraiProfileRevision>
{
    public void Configure(EntityTypeBuilder<KraiProfileRevision> builder)
    {
        builder.HasKey(e => e.KraiProfileRevisionKey);
        builder.HasIndex(e => new { e.KraiCode, e.Revision }).IsUnique();
        builder.Property(e => e.KraiCode).HasMaxLength(8);
        builder.Property(e => e.Note).HasMaxLength(500);
        builder.HasOne(e => e.Krai)
            .WithMany(k => k.ProfileRevisions)
            .HasForeignKey(e => e.KraiCode)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasData(KraiSeed.All.Select(seed => new KraiProfileRevision
        {
            KraiProfileRevisionKey = seed.RevisionKey,
            KraiCode = seed.Code,
            Revision = 1,
            SchemaVersion = 1,
            Json = seed.Profile,
            CreatedAtUtc = seed.SeededAtUtc,
            Note = "Початковий профіль"
        }));
    }
}
