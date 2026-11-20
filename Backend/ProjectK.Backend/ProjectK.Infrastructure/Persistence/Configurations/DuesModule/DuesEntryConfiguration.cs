using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ProjectK.Common.Entities.DuesModule;

namespace ProjectK.Infrastructure.Persistence.Configurations.DuesModule;

/// <summary>Held by key with no foreign key, as every dues table: see <see cref="KurinDuesRateConfiguration"/>.</summary>
internal sealed class DuesEntryConfiguration : IEntityTypeConfiguration<DuesEntry>
{
    public void Configure(EntityTypeBuilder<DuesEntry> builder)
    {
        builder.HasKey(e => e.DuesEntryKey);
        builder.Property(e => e.Amount).HasPrecision(18, 2);
        builder.Property(e => e.Kind).HasConversion<int>();
        builder.Property(e => e.Method).HasConversion<int>();
        builder.Property(e => e.CounterMethod).HasConversion<int?>();
        builder.Property(e => e.Note).HasMaxLength(500);
        builder.Ignore(e => e.IsVerified);
        builder.Ignore(e => e.IsDeleted);
        builder.HasIndex(e => new { e.KurinKey, e.GroupKey });
        builder.HasIndex(e => e.MembershipKey);
    }
}
