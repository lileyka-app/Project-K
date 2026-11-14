using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ProjectK.Common.Dtos.AuditModule;
using ProjectK.Common.Entities.AuditModule;

namespace ProjectK.Infrastructure.Persistence.Configurations.AuditModule;

/// <summary>
/// The journal holds everything by key and names by snapshot, with no foreign key anywhere:
/// an entry has to outlive the person, the гурток and the record it describes.
/// </summary>
internal sealed class KurinAuditEventConfiguration : IEntityTypeConfiguration<KurinAuditEvent>
{
    public void Configure(EntityTypeBuilder<KurinAuditEvent> builder)
    {
        builder.HasKey(e => e.KurinAuditEventKey);
        builder.Property(e => e.Area).HasConversion<int>();
        builder.Property(e => e.Action).HasMaxLength(KurinAuditEvent.ActionMaxLength).IsRequired();
        builder.Property(e => e.ActorName).HasMaxLength(KurinAuditEvent.NameMaxLength);
        builder.Property(e => e.ActorRole).HasMaxLength(KurinAuditEvent.RoleMaxLength).IsRequired();
        builder.Property(e => e.SubjectMemberName).HasMaxLength(KurinAuditEvent.NameMaxLength);
        builder.Property(e => e.SubjectLabel).HasMaxLength(KurinAuditEvent.LabelMaxLength).IsRequired();
        builder.Property(e => e.Note).HasMaxLength(KurinAuditEvent.NoteMaxLength);
        builder.Property(e => e.Changes)
            .HasConversion(
                changes => JsonSerializer.Serialize(changes, (JsonSerializerOptions?)null),
                json => JsonSerializer.Deserialize<List<AuditChange>>(json, (JsonSerializerOptions?)null) ?? new List<AuditChange>(),
                new ValueComparer<List<AuditChange>>(
                    (a, b) => JsonSerializer.Serialize(a, (JsonSerializerOptions?)null) == JsonSerializer.Serialize(b, (JsonSerializerOptions?)null),
                    changes => JsonSerializer.Serialize(changes, (JsonSerializerOptions?)null).GetHashCode(),
                    changes => JsonSerializer.Deserialize<List<AuditChange>>(JsonSerializer.Serialize(changes, (JsonSerializerOptions?)null), (JsonSerializerOptions?)null)!))
            .IsRequired();
        builder.HasIndex(e => new { e.KurinKey, e.OccurredAtUtc }).IsDescending(false, true);
        builder.HasIndex(e => new { e.SubjectMemberKey, e.OccurredAtUtc });
        builder.HasIndex(e => e.SubjectKey);
        builder.HasIndex(e => e.ActorUserKey);
        builder.HasIndex(e => e.OccurredAtUtc);
    }
}
