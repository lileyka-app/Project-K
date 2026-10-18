using System;
using System.ComponentModel.DataAnnotations;
using ProjectK.Common.Entities;

namespace ProjectK.Common.Entities.AuthModule;

public class WaitlistEntry : Entity
{
    [Key]
    public Guid WaitlistEntryKey { get; set; }

    [Required, MaxLength(100)]
    public string FirstName { get; set; } = null!;

    [Required, MaxLength(100)]
    public string LastName { get; set; } = null!;

    [Required, EmailAddress, MaxLength(256)]
    public string Email { get; set; } = null!;

    [Required, MaxLength(20)]
    public string PhoneNumber { get; set; } = null!;

    public DateTime DateOfBirth { get; set; }

    [MaxLength(120)]
    public string? Stanytsia { get; set; }

    [MaxLength(120)]
    public string? RegionOrCountry { get; set; }

    /// <summary>
    /// The край the applicant's kurin belongs to, picked from the list on the form; the admin may
    /// change it on approval. Empty on entries from before the form asked, and then the kurin opens
    /// in the instance's default край.
    /// </summary>
    [MaxLength(8)]
    public string? KraiCode { get; set; }

    public bool IsKurinLeaderCandidate { get; set; }

    [MaxLength(200)]
    public string? ClaimedKurinNameOrNumber { get; set; }

    /// <summary>
    /// The applicant leads a підготовчий курінь, which has no number yet: approval opens it without one.
    /// </summary>
    public bool IsPreparatoryKurin { get; set; }

    /// <summary>
    /// How the entry came to be: a зв'язковий applied through the form, or a провід added the person
    /// straight into a kurin and the entry exists only for their invitation to hang off.
    /// </summary>
    public WaitlistEntrySource Source { get; set; } = WaitlistEntrySource.Application;

    /// <summary>
    /// The kurin this applicant's approval opened for them, when they applied as its leader.
    /// Activation seats them in it. Written here, on the record that exists only for as long as
    /// the application does, rather than on the account — where it read as the kurin the person
    /// belonged to, which is membership's to say.
    /// </summary>
    public Guid? FoundedKurinKey { get; set; }

    public WaitlistVerificationStatus VerificationStatus { get; set; }

    [MaxLength(100)]
    public string? VerificationChannel { get; set; }

    [MaxLength(1000)]
    public string? VerificationNote { get; set; }

    public DateTime RequestedAtUtc { get; set; }
    public DateTime? ReviewedAtUtc { get; set; }
    public Guid? ReviewedByUserKey { get; set; }
    public DateTime? ApprovedAtUtc { get; set; }
    public DateTime? InvitationSentAtUtc { get; set; }
    public DateTime? InvitationAcceptedAtUtc { get; set; }
}

public enum WaitlistVerificationStatus
{
    Submitted = 0,
    NeedsManualVerification = 1,
    Verified = 2,
    Rejected = 3,
    ApprovedForInvitation = 4
}

public enum WaitlistEntrySource
{
    /// <summary>Submitted through the public waitlist form.</summary>
    Application = 0,

    /// <summary>Opened on someone's behalf when a провід gave a member an account directly.</summary>
    DirectAdd = 1
}
