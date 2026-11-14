using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ProjectK.Common.Entities.DuesModule;

namespace ProjectK.Infrastructure.Persistence.Configurations.DuesModule;

/// <summary>
/// Dues module. Money is decimal(18,2) everywhere; quarters are DuesQuarter.Index. Kurins, гуртки
/// and memberships are held by key with no foreign key: money outlives a deleted гурток.
/// </summary>
internal sealed class KurinDuesRateConfiguration : IEntityTypeConfiguration<KurinDuesRate>
{
    public void Configure(EntityTypeBuilder<KurinDuesRate> builder)
    {
        builder.HasKey(e => e.KurinDuesRateKey);
        builder.Property(e => e.StanytsiaFull).HasPrecision(18, 2);
        builder.Property(e => e.StanytsiaReduced).HasPrecision(18, 2);
        builder.Property(e => e.KurinShare).HasPrecision(18, 2);
        builder.Property(e => e.KurinReduced).HasPrecision(18, 2);
        builder.Property(e => e.KraiFull).HasPrecision(18, 2);
        builder.Property(e => e.KraiReduced).HasPrecision(18, 2);
        // A plain JSON column rather than an owned collection: rates are read untracked and saved
        // back with Update, which owned collections cannot do.
        builder.Property(e => e.Articles)
            .HasConversion(
                a => JsonSerializer.Serialize(a, (JsonSerializerOptions?)null),
                json => JsonSerializer.Deserialize<List<KurinDuesArticle>>(json, (JsonSerializerOptions?)null) ?? new List<KurinDuesArticle>(),
                new ValueComparer<List<KurinDuesArticle>>(
                    (a, b) => JsonSerializer.Serialize(a, (JsonSerializerOptions?)null) == JsonSerializer.Serialize(b, (JsonSerializerOptions?)null),
                    a => JsonSerializer.Serialize(a, (JsonSerializerOptions?)null).GetHashCode(),
                    a => JsonSerializer.Deserialize<List<KurinDuesArticle>>(JsonSerializer.Serialize(a, (JsonSerializerOptions?)null), (JsonSerializerOptions?)null)!));
        // One rate per tag per quarter, and one for everyone: the null tag is a value here, so the
        // index must not get EF's default IS NOT NULL filter.
        builder.HasIndex(e => new { e.KurinKey, e.TagKey, e.FromQuarter }).IsUnique().HasFilter(null);
    }
}
