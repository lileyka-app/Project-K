using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ProjectK.Common.Entities.KurinModule;
using ProjectK.Infrastructure.Persistence.Configurations.MemberModule;

namespace ProjectK.Infrastructure.Persistence.Configurations.KurinModule;

/// <summary>
/// Requests between kurins hold the person and both kurins by key: the record of who asked whom
/// outlives either side changing.
/// </summary>
internal sealed class MembershipRequestConfiguration : IEntityTypeConfiguration<MembershipRequest>
{
    public void Configure(EntityTypeBuilder<MembershipRequest> builder)
    {
        builder.HasKey(e => e.MembershipRequestKey);
        builder.Property(e => e.Kind).HasConversion<int>();
        builder.Property(e => e.Status).HasConversion<int>();
        builder.Property(e => e.MembershipKind).HasConversion<int>();
        builder.Property(e => e.ConcurrencyStamp).IsConcurrencyToken();
        builder.HasIndex(e => e.FromKurinKey);
        builder.HasIndex(e => e.ToKurinKey);
        builder.HasIndex(e => e.MemberKey);
    }
}
