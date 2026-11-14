namespace ProjectK.BusinessLogic.Modules.AuthModule.Models;

public class MigrationPreflightReport
{
    public List<DuplicateEmailConflict> DuplicateEmailConflicts { get; set; } = new();
    public List<OrphanMemberInfo> OrphanMembers { get; set; } = new();
    public List<OrphanUserInfo> OrphanUsers { get; set; } = new();
    public List<InconsistentLinkInfo> InconsistentLinks { get; set; } = new();
    public int TotalMembers { get; set; }
    public int TotalUsers { get; set; }
}

public record DuplicateEmailConflict(string Email, List<Guid> MemberKeys, List<Guid> UserKeys);
public record OrphanMemberInfo(Guid MemberKey, string Name, string Email, Guid? MissingUserKey);
public record OrphanUserInfo(Guid UserKey, string UserName, string Email);
public record InconsistentLinkInfo(Guid MemberKey, Guid UserKey, Guid MemberKurinKey, Guid? UserKurinKey);
