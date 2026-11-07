using System;
using ProjectK.Common.Models.Enums;

namespace ProjectK.Common.Models.Dtos.KurinModule;

public class MemberAwardDto
{
    public Guid MemberAwardKey { get; set; }
    public Guid MemberKey { get; set; }
    public Guid KurinKey { get; set; }
    /// <summary>An УПЮ відзначення's level; null for a senior one.</summary>
    public MemberAwardLevel? Level { get; set; }

    /// <summary>A senior відзначення's catalogue id; null for an УПЮ one.</summary>
    public string? CatalogId { get; set; }
    public DateTime DateAcquired { get; set; }
    public string? Note { get; set; }

    /// <summary>The person pinned it to the front of their card.</summary>
    public bool IsPinned { get; set; }
    public BadgeProgressStatus Status { get; set; }
    public string? ImageUrl { get; set; }
    public DateTime? SubmittedAtUtc { get; set; }
    public Guid? SubmittedByUserKey { get; set; }
    public DateTime? ReviewedAtUtc { get; set; }
    public Guid? ReviewedByUserKey { get; set; }
}
