using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ProjectK.Common.Entities.IdentityModule;
using ProjectK.Common.Entities.KraiModule;
using ProjectK.Common.Entities.KurinModule;

namespace ProjectK.Infrastructure.Persistence.Configurations.IdentityModule;

internal sealed class WaitlistEntryConfiguration : IEntityTypeConfiguration<WaitlistEntry>
{
    public void Configure(EntityTypeBuilder<WaitlistEntry> builder)
    {
        builder.HasKey(e => e.WaitlistEntryKey);
        builder.Ignore(e => e.ClaimedSeniorBranches);
        builder.Property(e => e.Stanytsia)
            .HasMaxLength(120);
        builder.Property(e => e.RegionOrCountry)
            .HasMaxLength(120);
        builder.Property(e => e.KraiCode)
            .HasMaxLength(8);
        builder.HasOne<Krai>()
            .WithMany()
            .HasForeignKey(e => e.KraiCode)
            .OnDelete(DeleteBehavior.Restrict);
        builder.Property(e => e.VerificationStatus)
            .HasConversion<string>();
        builder.Property(e => e.Source)
            .HasConversion<string>();
        builder.HasIndex(e => e.Email).IsUnique();
        // The kurin a founder's approval opened; it goes with the kurin, not with the entry.
        builder.HasOne<Kurin>()
            .WithMany()
            .HasForeignKey(e => e.FoundedKurinKey)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
