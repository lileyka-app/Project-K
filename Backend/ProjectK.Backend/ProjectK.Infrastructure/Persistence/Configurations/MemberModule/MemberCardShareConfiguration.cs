using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ProjectK.Common.Entities.MemberModule;

namespace ProjectK.Infrastructure.Persistence.Configurations.MemberModule;

internal sealed class MemberCardShareConfiguration : IEntityTypeConfiguration<MemberCardShare>
{
    public void Configure(EntityTypeBuilder<MemberCardShare> builder)
    {
        builder.HasKey(e => e.MemberCardShareKey);
        builder.Property(e => e.Fields).HasConversion<int>();
        builder.Property(e => e.LinkToken).HasMaxLength(64);
        builder.HasIndex(e => e.MemberKey).IsUnique();
        builder.HasIndex(e => e.LinkToken).IsUnique().HasFilter("[LinkToken] IS NOT NULL");
    }
}
