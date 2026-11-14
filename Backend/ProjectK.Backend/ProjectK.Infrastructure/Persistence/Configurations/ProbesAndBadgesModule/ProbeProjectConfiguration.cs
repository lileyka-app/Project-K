using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ProjectK.Common.Entities.ProbesAndBadgesModule;
using ProjectK.Common.Models.Records;

namespace ProjectK.Infrastructure.Persistence.Configurations.ProbesAndBadgesModule;

internal sealed class ProbeProjectConfiguration : IEntityTypeConfiguration<ProbeProject>
{
    public void Configure(EntityTypeBuilder<ProbeProject> builder)
    {
        const int shortText = ProbeProjectRules.MaxShortTextLength;
        builder.HasKey(e => e.ProbeProjectKey);
        builder.Property(e => e.ProbeId).HasMaxLength(200).IsRequired();
        builder.Property(e => e.Title).HasMaxLength(shortText).IsRequired();
        builder.Property(e => e.Area).HasConversion<int?>();
        builder.Property(e => e.Reach).HasConversion<int>();
        builder.Property(e => e.SharedWith).HasMaxLength(shortText);
        builder.Property(e => e.Location).HasMaxLength(shortText);
        builder.Property(e => e.EventLeads).HasMaxLength(shortText);
        builder.Property(e => e.CoordinatorName).HasMaxLength(shortText);
        builder.Property(e => e.CoordinatorContact).HasMaxLength(shortText);
        builder.Property(e => e.Description).HasMaxLength(ProbeProjectRules.MaxTextLength).IsRequired();
        builder.Property(e => e.Goal).HasMaxLength(ProbeProjectRules.MaxTextLength).IsRequired();
        builder.Property(e => e.Plan).HasMaxLength(ProbeProjectRules.MaxTextLength).IsRequired();
        builder.Property(e => e.Results).HasMaxLength(ProbeProjectRules.MaxTextLength).IsRequired();
        builder.Property(e => e.CoordinatorComment).HasMaxLength(ProbeProjectRules.MaxCommentLength);
        builder.Property(e => e.ApprovedByName).HasMaxLength(200);
        builder.Property(e => e.CompletedByName).HasMaxLength(200);
        builder.Property(e => e.UpdatedByName).HasMaxLength(200);
        builder.HasIndex(e => new { e.MemberKey, e.ProbeId });
        builder.HasMany(e => e.Images)
            .WithOne(i => i.Project)
            .HasForeignKey(i => i.ProbeProjectKey)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
