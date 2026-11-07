using ProjectK.Common.Models.Members;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ProjectK.Common.Entities;
using ProjectK.Common.Entities.AuthModule;
using ProjectK.Common.Entities.KurinModule;
using ProjectK.Common.Models.Dtos;
using ProjectK.Common.Models.Enums;

namespace ProjectK.Common.Entities.KurinModule;

/// <summary>
/// A person. Not a person <i>of</i> anywhere — where they belong is said by
/// <see cref="Membership"/>, one row per kurin, and a person may hold several at once or none at
/// all. Everything here is theirs and outlives any of those: leaving a kurin, or the kurin
/// itself being deleted, leaves this record and its history untouched.
/// </summary>
public class Member : Entity, IHasParentContacts
{
    public Guid MemberKey { get; set; } = Guid.NewGuid();

    /// <summary>
    /// The code this person shares so another kurin can find them. Drawn at random when the member
    /// is first saved, never derived from <see cref="MemberKey"/> — see <c>MemberPublicId</c>.
    /// </summary>
    public string PublicId { get; set; } = string.Empty;
    public Guid? UserKey { get; set; }
    public string FirstName { get; set; }
    public string? MiddleName { get; set; }
    public string LastName { get; set; }
    public string Email { get; set; }
    public string PhoneNumber { get; set; }
    public DateOnly DateOfBirth { get; set; }
    public string? Address { get; set; }
    public string? School { get; set; }

    // Parents' contacts, as a kurin reaches a child's family. Private like the address.
    public string? MotherFirstName { get; set; }
    public string? MotherLastName { get; set; }
    public string? MotherPhone { get; set; }
    public string? FatherFirstName { get; set; }
    public string? FatherLastName { get; set; }
    public string? FatherPhone { get; set; }

    /// <summary>
    /// Values of the card fields a край adds, as a JSON object by field key; see
    /// <c>MemberCardExtras</c>. Kept when the край drops a field, so bringing it back loses nothing.
    /// </summary>
    public string? CardExtras { get; set; }
    public string? ProfilePhotoBlobName { get; set; }
    public MemberProfileVerificationStatus ProfileVerificationStatus { get; set; } = MemberProfileVerificationStatus.Unverified;
    public DateTime? ProfileVerifiedAtUtc { get; set; }
    public Guid? ProfileVerifiedByUserKey { get; set; }
    public string? ProfileVerificationNote { get; set; }
    public PlastLevel? LatestPlastLevel { get; set; }
    public ICollection<PlastLevelHistory> PlastLevelHistory { get; set; } = new List<PlastLevelHistory>();
    public ICollection<LeadershipHistory> LeadershipHistories { get; set; } = new List<LeadershipHistory>();
    public ICollection<MemberWarning> MemberWarnings { get; set; } = new List<MemberWarning>();
    public ICollection<MemberAward> MemberAwards { get; set; } = new List<MemberAward>();
    public AppUser? User { get; set; }
}
