using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ProjectK.Common.Entities.ProbesAndBadgesModule;
using ProjectK.Common.Rules.ProbesAndBadgesModule;

namespace ProjectK.Infrastructure.Persistence.Configurations.ProbesAndBadgesModule;

internal sealed class ThirdProbeFileConfiguration : IEntityTypeConfiguration<ThirdProbeFile>
{
    public void Configure(EntityTypeBuilder<ThirdProbeFile> builder)
    {
        const int shortText = ProbeProjectRules.MaxShortTextLength;
        builder.HasKey(e => e.ThirdProbeFileKey);
        builder.Property(e => e.ProbeId).HasMaxLength(200).IsRequired();
        builder.Property(e => e.MentorZvyazkovyi).HasMaxLength(shortText);
        builder.Property(e => e.MentorVporiadnyk).HasMaxLength(shortText);
        builder.Property(e => e.MentorReferent).HasMaxLength(shortText);
        builder.Property(e => e.ReflectionWith).HasMaxLength(shortText);
        builder.Property(e => e.ReflectionConfirmedByName).HasMaxLength(200);
        builder.Property(e => e.StagesUpdatedByName).HasMaxLength(200);
        builder.Property(e => e.CampKind).HasConversion<int?>();
        builder.Property(e => e.CampName).HasMaxLength(shortText);
        builder.Property(e => e.CampCertificateBlobName).HasMaxLength(300);
        builder.Property(e => e.CampCertificateContentType).HasMaxLength(100);
        builder.Property(e => e.CampCertificateFileName).HasMaxLength(shortText);
        builder.Property(e => e.PhysicalBadgeId).HasMaxLength(200);
        builder.Property(e => e.Resume).HasMaxLength(ProbeProjectRules.MaxResumeLength).IsRequired();
        builder.Property(e => e.UpdatedByName).HasMaxLength(200);
        builder.HasIndex(e => new { e.MemberKey, e.ProbeId }).IsUnique();
    }
}
