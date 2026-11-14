using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ProjectK.Common.Entities.DuesModule;

namespace ProjectK.Infrastructure.Persistence.Configurations.DuesModule;

/// <summary>Held by key with no foreign key, as every dues table: see <see cref="KurinDuesRateConfiguration"/>.</summary>
internal sealed class DuesChargeConfiguration : IEntityTypeConfiguration<DuesCharge>
{
    public void Configure(EntityTypeBuilder<DuesCharge> builder)
    {
        builder.HasKey(e => e.DuesChargeKey);
        builder.HasIndex(e => new { e.MembershipKey, e.Quarter }).IsUnique();
        builder.HasIndex(e => e.KurinKey);
    }
}
