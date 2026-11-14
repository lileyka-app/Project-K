using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ProjectK.Common.Entities.KurinModule;
using ProjectK.Common.Enums.KurinModule;

namespace ProjectK.Infrastructure.Persistence.Configurations.KurinModule;

internal sealed class KurinBranchRequestConfiguration : IEntityTypeConfiguration<KurinBranchRequest>
{
    public void Configure(EntityTypeBuilder<KurinBranchRequest> builder)
    {
        builder.HasKey(e => e.KurinBranchRequestKey);
        builder.Property(e => e.Status).HasConversion<int>();
        builder.Property(e => e.Note).HasMaxLength(KurinBranchRequest.NoteMaxLength);
        builder.Ignore(e => e.WantedBranches);
        builder.HasIndex(e => new { e.KurinKey, e.Status });
        // A kurin keeps one request open: the submit replaces it, and this stops two at once.
        builder.HasIndex(e => e.KurinKey)
            .IsUnique()
            .HasFilter($"[Status] = {(int)KurinBranchRequestStatus.Pending}")
            .HasDatabaseName("IX_KurinBranchRequests_KurinKey_Pending");
        builder.HasOne<Kurin>()
            .WithMany()
            .HasForeignKey(e => e.KurinKey)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
