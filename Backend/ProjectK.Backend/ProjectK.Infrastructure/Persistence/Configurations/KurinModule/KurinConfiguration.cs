using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ProjectK.Common.Entities.KraiModule;
using ProjectK.Common.Entities.KurinModule;

namespace ProjectK.Infrastructure.Persistence.Configurations.KurinModule;

internal sealed class KurinConfiguration : IEntityTypeConfiguration<Kurin>
{
    public void Configure(EntityTypeBuilder<Kurin> builder)
    {
        builder.Property(e => e.Branch).HasConversion<int>();
        builder.HasKey(e => e.KurinKey);
        builder.Property(e => e.KraiCode)
            .HasMaxLength(8)
            .HasDefaultValue(Krai.Ukraine);
        builder.HasOne<Krai>()
            .WithMany()
            .HasForeignKey(e => e.KraiCode)
            .OnDelete(DeleteBehavior.Restrict);
        // A number is the kurin's within its край and гілка: two краї can each have a kurin 12,
        // and a УПЮ kurin 5 stands beside a УСП kurin 5. This guards the main гілка; a second
        // гілка's number is checked by the handlers that set it (IKurinRepository.IsNumberTakenAsync).
        builder.HasIndex(e => new { e.KraiCode, e.Branch, e.Number }).IsUnique();
        builder.HasMany(e => e.Branches)
            .WithOne()
            .HasForeignKey(b => b.KurinKey)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(e => e.Branches).AutoInclude();
        builder.Ignore(e => e.Numbering);
        builder.Ignore(e => e.IsScoreEnabled);
        builder.Property(e => e.RoleNames).HasMaxLength(2000);
        builder.Property(e => e.EmblemBlobName).HasMaxLength(512);
        builder.Property(e => e.Stanytsia)
            .HasMaxLength(120);
        builder.Property(e => e.RegionOrCountry)
            .HasMaxLength(120);
        builder.Property(e => e.NamedAfter)
            .HasMaxLength(200);
        builder.Property(e => e.Description)
            .HasMaxLength(4000);
    }
}
