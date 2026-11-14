using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ProjectK.Common.Entities.AuthModule;

namespace ProjectK.Infrastructure.Persistence.Configurations.AuthModule;

internal sealed class DismissedDutyConfiguration : IEntityTypeConfiguration<DismissedDuty>
{
    public void Configure(EntityTypeBuilder<DismissedDuty> builder)
    {
        builder.HasKey(e => e.DismissedDutyKey);
        builder.Property(e => e.DutyKey)
            .HasMaxLength(DismissedDuty.DutyKeyMaxLength)
            .IsRequired();
        builder.Property(e => e.Fingerprint)
            .HasMaxLength(DismissedDuty.FingerprintMaxLength)
            .IsRequired();
        builder.HasOne(e => e.User)
            .WithMany()
            .HasForeignKey(e => e.UserKey)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(e => new { e.UserKey, e.DutyKey }).IsUnique();
        builder.HasIndex(e => e.DismissedAtUtc);
    }
}
