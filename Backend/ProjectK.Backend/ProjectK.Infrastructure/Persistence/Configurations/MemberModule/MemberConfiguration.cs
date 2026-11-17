using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ProjectK.Common.Entities.IdentityModule;
using ProjectK.Common.Entities.MemberModule;

namespace ProjectK.Infrastructure.Persistence.Configurations.MemberModule;

internal sealed class MemberConfiguration : IEntityTypeConfiguration<Member>
{
    public void Configure(EntityTypeBuilder<Member> builder)
    {
        builder.HasKey(e => e.MemberKey);
        builder.Property(e => e.PublicId)
            .HasMaxLength(20)
            .IsRequired();
        builder.HasIndex(e => e.PublicId)
            .IsUnique();
        builder.HasOne<AppUser>()
            .WithOne()
            .HasForeignKey<Member>(e => e.UserKey)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
