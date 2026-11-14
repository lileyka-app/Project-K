using System.ComponentModel.DataAnnotations;

namespace ProjectK.Common.Entities.AuthModule;

public class Invitation : Entity
{
    [Key]
    public Guid InvitationKey { get; set; }

    [Required, MaxLength(100)]
    public string Token { get; set; } = null!;

    public Guid WaitlistEntryKey { get; set; }
    public WaitlistEntry WaitlistEntry { get; set; } = null!;

    public Guid? TargetUserKey { get; set; }
    public AppUser? TargetUser { get; set; }

    public DateTime ExpiresAtUtc { get; set; }
    public DateTime? UsedAtUtc { get; set; }

    public bool IsRevoked { get; set; }
}
