using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ProjectK.Common.Entities.DuesModule;

namespace ProjectK.Infrastructure.Persistence.Configurations.DuesModule;

/// <summary>Held by key with no foreign key, as every dues table: see <see cref="KurinDuesRateConfiguration"/>.</summary>
internal sealed class GroupDuesRateConfiguration : IEntityTypeConfiguration<GroupDuesRate>
{
    public void Configure(EntityTypeBuilder<GroupDuesRate> builder)
    {
        builder.HasKey(e => e.GroupDuesRateKey);
        builder.Property(e => e.GroupShare).HasPrecision(18, 2);
        builder.HasIndex(e => new { e.GroupKey, e.FromQuarter }).IsUnique();
        builder.HasIndex(e => e.KurinKey);
    }
}
