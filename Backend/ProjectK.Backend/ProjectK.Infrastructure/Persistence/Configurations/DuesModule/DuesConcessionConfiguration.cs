using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ProjectK.Common.Entities.DuesModule;

namespace ProjectK.Infrastructure.Persistence.Configurations.DuesModule;

/// <summary>Held by key with no foreign key, as every dues table: see <see cref="KurinDuesRateConfiguration"/>.</summary>
internal sealed class DuesConcessionConfiguration : IEntityTypeConfiguration<DuesConcession>
{
    public void Configure(EntityTypeBuilder<DuesConcession> builder)
    {
        builder.HasKey(e => e.DuesConcessionKey);
        builder.HasIndex(e => new { e.MembershipKey, e.FromQuarter }).IsUnique();
        builder.HasIndex(e => e.KurinKey);
    }
}
