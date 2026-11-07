using System;
using ProjectK.Common.Entities;
using ProjectK.Common.Models.Enums;

namespace ProjectK.Common.Entities.KurinModule;

public class MemberAward : Entity
{
    public Guid MemberAwardKey { get; set; } = Guid.NewGuid();
    public Guid MemberKey { get; set; }
    public Guid KurinKey { get; set; }
    public Member Member { get; set; } = null!;
    public const int CatalogIdMaxLength = 100;

    /// <summary>An УПЮ відзначення's level; null for a senior one, which goes by <see cref="CatalogId"/>.</summary>
    public MemberAwardLevel? Level { get; set; }

    /// <summary>
    /// A senior відзначення (грамота, орден, хрест) by its catalogue id; null for an УПЮ one. The person
    /// records it themself or the senior kurin's провід does, and it stands without confirmation.
    /// </summary>
    public string? CatalogId { get; set; }
    public DateTime DateAcquired { get; set; }
    /// <summary>What it was given for, in the words of whoever recorded it.</summary>
    public string? Note { get; set; }

    /// <summary>
    /// The person pinned this відзначення to the front of their card. Set on every grant of it at
    /// once (PinMyAwardCommand), so the card reads a відзначення as pinned when any grant is.
    /// </summary>
    public bool IsPinned { get; set; }
    public BadgeProgressStatus Status { get; set; } = BadgeProgressStatus.Confirmed;
    public DateTime? SubmittedAtUtc { get; set; }
    public Guid? SubmittedByUserKey { get; set; }
    public DateTime? ReviewedAtUtc { get; set; }
    public Guid? ReviewedByUserKey { get; set; }
}
