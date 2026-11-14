using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ProjectK.Common.Entities.KurinModule;

namespace ProjectK.Infrastructure.Persistence.Configurations.KurinModule;

internal sealed class KurinBranchNumberConfiguration : IEntityTypeConfiguration<KurinBranchNumber>
{
    public void Configure(EntityTypeBuilder<KurinBranchNumber> builder)
    {
        builder.ToTable("KurinBranches");
        builder.HasKey(e => new { e.KurinKey, e.Branch });
        builder.Property(e => e.Branch).HasConversion<int>();
        builder.Property(e => e.KraiCode).HasMaxLength(8);
        // Every number of every гілка is the kurin's alone in its край, the second гілка too:
        // two approvals at once cannot both take УПС 12.
        builder.HasIndex(e => new { e.KraiCode, e.Branch, e.Number }).IsUnique();
    }
}
