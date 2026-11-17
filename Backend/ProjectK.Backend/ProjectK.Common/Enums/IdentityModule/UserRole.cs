namespace ProjectK.Common.Enums.IdentityModule;

// System-level roles only. Kurin authority is no longer modelled here — it comes from
// діловодські offices (see ProjectK.Common.Rules.IdentityModule.SystemRoleNames / RolePermissionRules).
public enum UserRole
{
    Admin,
    Member
}
