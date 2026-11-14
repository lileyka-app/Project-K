using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ProjectK.Common.Entities.AuthModule;

namespace ProjectK.Infrastructure.Persistence.Configurations.AuthModule;

internal sealed class InvitationConfiguration : IEntityTypeConfiguration<Invitation>
{
    public void Configure(EntityTypeBuilder<Invitation> builder)
    {
        builder.HasKey(e => e.InvitationKey);
        builder.HasOne(e => e.WaitlistEntry)
            .WithMany()
            .HasForeignKey(e => e.WaitlistEntryKey)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(e => e.TargetUser)
            .WithMany()
            .HasForeignKey(e => e.TargetUserKey)
            .OnDelete(DeleteBehavior.SetNull);
        builder.HasIndex(e => e.Token).IsUnique();
    }
}
