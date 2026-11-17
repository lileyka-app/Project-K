using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ProjectK.Common.Entities.IdentityModule;
using ProjectK.Common.Entities.KurinModule;

namespace ProjectK.Infrastructure.Persistence.Configurations.IdentityModule;

/// <summary>
/// The kurin an account stepped into. With the database forgetting it alongside the kurin,
/// no new path of deletion can leave an account scoped to a kurin that is gone (STAB-05).
/// </summary>
internal sealed class AppUserConfiguration : IEntityTypeConfiguration<AppUser>
{
    public void Configure(EntityTypeBuilder<AppUser> builder)
    {
        builder.HasOne<Kurin>()
            .WithMany()
            .HasForeignKey(user => user.ActiveKurinKey)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
