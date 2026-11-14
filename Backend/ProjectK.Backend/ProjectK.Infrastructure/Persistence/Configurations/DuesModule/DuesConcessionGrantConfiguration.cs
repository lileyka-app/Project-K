using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ProjectK.Common.Entities.DuesModule;

namespace ProjectK.Infrastructure.Persistence.Configurations.DuesModule;

/// <summary>Held by key with no foreign key, as every dues table: see <see cref="KurinDuesRateConfiguration"/>.</summary>
internal sealed class DuesConcessionGrantConfiguration : IEntityTypeConfiguration<DuesConcessionGrant>
{
    public void Configure(EntityTypeBuilder<DuesConcessionGrant> builder)
    {
        builder.HasKey(e => e.DuesConcessionGrantKey);
        builder.HasIndex(e => e.KurinKey);
        builder.HasIndex(e => e.MembershipKey);
        builder.Property(e => e.ConcessionKey).HasMaxLength(40);
        builder.Property(e => e.Name).HasMaxLength(80);
        builder.Property(e => e.Kind).HasMaxLength(16);
        builder.Property(e => e.Value).HasPrecision(18, 2);
        builder.Property(e => e.Parts).HasConversion<int>();
        builder.Property(e => e.Reason).HasMaxLength(500);
    }
}
