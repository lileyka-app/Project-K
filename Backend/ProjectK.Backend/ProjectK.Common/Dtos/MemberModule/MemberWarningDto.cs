using ProjectK.Common.Enums.MemberModule;


namespace ProjectK.Common.Dtos.MemberModule;

public class MemberWarningDto
{
    public Guid MemberWarningKey { get; set; }
    public Guid MemberKey { get; set; }
    public MemberWarningLevel Level { get; set; }
    public DateTime IssuedAtUtc { get; set; }
    public DateTime ExpiresAtUtc { get; set; }
    public Guid IssuedByUserKey { get; set; }
    public Guid? RevokedByUserKey { get; set; }
    public DateTime? RevokedAtUtc { get; set; }
}
