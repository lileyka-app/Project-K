using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ProjectK.Common.Entities.MemberModule;

namespace ProjectK.Infrastructure.Persistence.Configurations.MemberModule;

internal sealed class PlastLevelHistoryConfiguration : IEntityTypeConfiguration<PlastLevelHistory>
{
    public void Configure(EntityTypeBuilder<PlastLevelHistory> builder)
    {
        builder.HasKey(e => e.PlastLevelHistoryKey);
        builder.HasOne(e => e.Member)
            .WithMany(m => m.PlastLevelHistory)
            .HasForeignKey(e => e.MemberKey)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Property(e => e.PlastLevel)
            .HasConversion<int>();
    }
}
