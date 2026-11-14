using ProjectK.Common.Enums.MemberModule;


namespace ProjectK.Common.Dtos.MemberModule;

public class MemberLookupDto
{
    public Guid MemberKey { get; set; }
    public Guid? UserKey { get; set; }
    public string FirstName { get; set; }
    public string? MiddleName { get; set; }
    public string LastName { get; set; }
    public string? UserRole { get; set; }
    public MemberProfileVerificationStatus ProfileVerificationStatus { get; set; }
    public DateTime? ProfileVerifiedAtUtc { get; set; }
    public Guid? ProfileVerifiedByUserKey { get; set; }
    public string? ProfileVerificationNote { get; set; }

    /// <summary>A signed link to the person's photo, or null when they have none.</summary>
    public string? ProfilePhotoUrl { get; set; }

    /// <summary>
    /// The stored blob behind <see cref="ProfilePhotoUrl"/>. Repositories cannot sign links, so they
    /// fill this and the handler turns it into the link; it never reaches the client.
    /// </summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public string? ProfilePhotoBlobName { get; set; }
}
